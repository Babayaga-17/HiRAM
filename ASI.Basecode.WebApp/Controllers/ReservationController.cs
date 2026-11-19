// ASI.Basecode.WebApp/Controllers/ReservationController.cs
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Resources.EmailTemplates;
using ASI.Basecode.Services.Exceptions;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.ServiceModels.Equipment;
using ASI.Basecode.Services.ServiceModels.Reservation;
using ASI.Basecode.Services.Services;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers;

[Authorize(Roles = $"{RoleNames.Borrower}, {RoleNames.Custodian}, {RoleNames.Admin}")]
public class ReservationController : ControllerBase<ReservationController>
{
    private const string SelectionKey = "SelectedEquipmentIds";

    private readonly IReservationService _reservationService;
    private readonly IEquipmentService _equipmentService;
    private readonly IEmailManager _emailManager;

    public ReservationController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IReservationService reservationService,
        IEquipmentService equipmentService,
        IEmailManager emailManager,
        IMapper mapper = null) : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _reservationService = reservationService;
        _equipmentService = equipmentService;
        _emailManager = emailManager;
    }

    #region BORROWER Actions

    [HttpGet]
    [Authorize(Roles = RoleNames.Borrower)]
    public async Task<IActionResult> MyReservation(string status = null, int page = 1, int pageSize = 5)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var borrowerUserId) || borrowerUserId <= 0)
            return Challenge();

        try
        {
            var reservations = await _reservationService.GetBorrowerReservationsAsync(borrowerUserId, status, page, pageSize);
            return View(reservations);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load borrower reservations.");
            return View(new MyReservationPageViewModel
            {
                IsAvailable = false,
                Status = MyReservationPageViewModel.StatusOptions.FirstOrDefault(value =>
                    string.Equals(value, status?.Trim(), StringComparison.OrdinalIgnoreCase)),
                PageSize = pageSize == 10 || pageSize == 20 ? pageSize : 5
            });
        }
    }

    [HttpGet]
    [Authorize(Roles = RoleNames.Borrower)]
    public IActionResult Create()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var borrowerUserId) || borrowerUserId <= 0)
        {
            return Challenge();
        }

        var draft = HttpContext.Session.GetString("ReservationDraft:Create");

        var defaultPickup = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8))
            .DateTime
            .AddMinutes(15);

        var model = draft == null
            ? new ReservationFormModel()
            {
                Pickup = defaultPickup,
                ExpectedReturn = defaultPickup.AddDays(1)
            }
            : JsonSerializer.Deserialize<ReservationFormModel>(draft);

        model.EquipmentIds = ParseSelection(HttpContext.Session.GetString(SelectionKey));

        // A draft may contain a previous preview.
        // Always rebuild it from current availability.
        model.AvailabilityPreview = null;

        try
        {
            LoadEquipment(model);

            if (model.EquipmentIds.Count > 0 &&
                model.Pickup.HasValue &&
                model.ExpectedReturn.HasValue &&
                model.ExpectedReturn.Value > model.Pickup.Value)
            {
                model.AvailabilityPreview =
                    _reservationService.GetAvailabilityPreview(
                        model, borrowerUserId);
            }
        }
        catch (ReservationValidationException exception)
        {
            AddReservationErrors(exception);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to load the initial reservation preview for borrower {UserId}.",
                borrowerUserId);

            ModelState.AddModelError(
                string.Empty,
                "Unable to load availability. You can try Check availability again.");
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RoleNames.Borrower)]
    public async Task<IActionResult> Create([Bind("EquipmentIds,Pickup,ExpectedReturn,Purpose")] ReservationFormModel model)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var borrowerUserId) || borrowerUserId <= 0)
            return Challenge();

        if (!ModelState.IsValid)
        {
            LoadEquipment(model);
            return View(model);
        }

        string reservationNumber = null;

        try
        {
            var result = _reservationService.Submit(model, borrowerUserId);
            if (result.Error != null)
                ModelState.AddModelError(string.Empty, result.Error);
            else
                reservationNumber = result.ReservationNumber;
        }
        catch (ReservationValidationException exception)
        {
            AddReservationErrors(exception);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to submit reservation for borrower {UserId}.", borrowerUserId);
            ModelState.AddModelError(string.Empty,"Your reservation could not be completed. Please try again; " + "availability will be checked again.");
        }

        if (reservationNumber == null)
        {
            LoadEquipment(model);
            return View(model);
        }

        // Reservation has been saved successfully.
        HttpContext.Session.Remove(SelectionKey);
        HttpContext.Session.Remove("ReservationDraft:Create");
        TempData["ReservationNumber"] = reservationNumber;

        await SendReservationEmailAsync(User.FindFirstValue(ClaimTypes.Email),
            $"HiRAM reservation received - {reservationNumber}",
            ReservationEmailTemplates.SubmissionBody(
                reservationNumber,
                model.Pickup.Value,
                model.ExpectedReturn.Value),
            "Your request was saved, but the confirmation email could not be sent. " + "You can track its status in My Reservations.");

        return RedirectToAction(nameof(Submitted));
    }

    [HttpGet]
    [Authorize(Roles = RoleNames.Borrower)]
    public IActionResult Edit(int id, string returnUrl = null)
    {
        returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        ViewData["ReservationReturnUrl"] = returnUrl;

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var borrowerUserId))
            return Challenge();

        var reservation = _reservationService.GetReservationDetails(id, borrowerUserId);

        if (reservation == null)
            return NotFound();

        if (reservation.Status != ReservationStatuses.Pending)
        {
            TempData["ReservationError"] = "Only pending reservations can be edited.";

            return RedirectToAction(nameof(ReservationDetails), new { id, returnUrl });
        }

        var draftKey = $"ReservationDraft:Edit:{id}";
        var selectionKey = $"SelectedEquipmentIds:Edit:{id}";

        var draft = HttpContext.Session.GetString(draftKey);

        var model = draft == null
            ? new ReservationEditFormModel
            {
                Pickup = reservation.StartDateTime,
                ExpectedReturn = reservation.ExpectedReturnDateTime,
                Purpose = reservation.Purpose,
                EquipmentIds = reservation.Equipment
                    .Select(item => item.EquipmentId)
                    .ToList(),
                RowVersion = Convert.ToBase64String(reservation.RowVersion)
            }
            : JsonSerializer.Deserialize<ReservationEditFormModel>(draft);

        var selection = HttpContext.Session.GetString(selectionKey);

        if (selection == null)
        {
            HttpContext.Session.SetString(selectionKey, string.Join(",", model.EquipmentIds));
        }
        else
        {
            model.EquipmentIds = ParseSelection(selection);
        }

        PrepareEditView(id, model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RoleNames.Borrower)]
    public IActionResult Edit(int id, [Bind("EquipmentIds,Pickup,ExpectedReturn,Purpose,RowVersion")] ReservationEditFormModel model, string returnUrl = null)
    {
        returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        ViewData["ReservationReturnUrl"] = returnUrl;
        if (!int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var borrowerUserId))
            return Challenge();

        var reservation =
            _reservationService.GetReservationDetails(id, borrowerUserId);

        if (reservation == null)
            return NotFound();

        if (reservation.Status != ReservationStatuses.Pending)
        {
            TempData["ReservationError"] =
                "Only pending reservations can be edited.";

            return RedirectToAction(nameof(ReservationDetails), new { id, returnUrl });
        }

        if (ModelState.IsValid)
        {
            try
            {
                var error = _reservationService.Edit(id, model, borrowerUserId);

                if (error == null)
                {
                    HttpContext.Session.Remove($"ReservationDraft:Edit:{id}");
                    HttpContext.Session.Remove($"SelectedEquipmentIds:Edit:{id}");

                    TempData["ReservationSuccess"] = "Reservation updated.";

                    return RedirectToAction(nameof(ReservationDetails), new { id, returnUrl });
                }

                ModelState.AddModelError(string.Empty, error);
            }
            catch (ReservationValidationException exception)
            {
                AddReservationErrors(exception);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception,
                    "Unable to edit reservation {ReservationId}.", id);

                ModelState.AddModelError(string.Empty,
                    "Unable to save your changes. Reload the reservation and try again.");
            }
        }

        PrepareEditView(id, model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RoleNames.Borrower)]
    public IActionResult ChangeEquipment(int? id, [Bind("EquipmentIds,Pickup,ExpectedReturn,Purpose,RowVersion")] ReservationEditFormModel model)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var borrowerUserId))
            return Challenge();

        if (id.HasValue)
        {
            var reservation = _reservationService.GetReservationDetails(id.Value, borrowerUserId);

            if (reservation == null)
                return NotFound();

            if (reservation.Status != ReservationStatuses.Pending)
            {
                TempData["ReservationError"] ="Only pending reservations can be edited.";

                return RedirectToAction(nameof(ReservationDetails), new { id = id.Value });
            }

            HttpContext.Session.SetString($"ReservationDraft:Edit:{id.Value}", JsonSerializer.Serialize(model));
            HttpContext.Session.SetString($"SelectedEquipmentIds:Edit:{id.Value}", string.Join(",", model.EquipmentIds));
        }
        else
        {
            HttpContext.Session.SetString("ReservationDraft:Create", JsonSerializer.Serialize<ReservationFormModel>(model));
            HttpContext.Session.SetString(SelectionKey, string.Join(",", model.EquipmentIds));
        }

        return RedirectToAction("Index", "BrowseEquipment", new { reservationId = id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RoleNames.Borrower)]
    public IActionResult CheckAvailability(int? id, [Bind("EquipmentIds,Pickup,ExpectedReturn,Purpose,RowVersion")] ReservationEditFormModel model, string returnUrl = null)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var borrowerUserId) || borrowerUserId <= 0)
        {
            return Challenge();
        }

        ViewData["ReservationReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

        if (id.HasValue)
            ViewData["ReservationId"] = id.Value;

        // Purpose is needed for submission, not an availability check.
        ModelState.Remove(nameof(model.Purpose));

        if (!id.HasValue)
            ModelState.Remove(nameof(model.RowVersion));

        // Create must receive a base model so the shared view
        // does not mistake it for an Edit form.
        ReservationFormModel form = id.HasValue
            ? model
            : new ReservationFormModel
        {
            EquipmentIds = model.EquipmentIds,
            Pickup = model.Pickup,
            ExpectedReturn = model.ExpectedReturn,
            Purpose = model.Purpose
        };

        try
        {
            if (id.HasValue)
            {
                var reservation = _reservationService.GetReservationDetails(id.Value, borrowerUserId);

                if (reservation == null)
                    return NotFound();

                if (reservation.Status != ReservationStatuses.Pending)
                {
                    TempData["ReservationError"] = "Only pending reservations can be edited.";

                    return RedirectToAction(nameof(ReservationDetails),
                        new
                        {
                            id = id.Value,
                            returnUrl =
                            ViewData["ReservationReturnUrl"]
                        });
                }
            }

            if (id.HasValue)
                PrepareEditView(id.Value, model);
            else
                LoadEquipment(form);

            if (ModelState.IsValid)
            {
                form.AvailabilityPreview = _reservationService.GetAvailabilityPreview(form, borrowerUserId, id);
            }
        }
        catch (ReservationValidationException exception)
        {
            AddReservationErrors(exception);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to check reservation availability for borrower {UserId}.", borrowerUserId);
            ModelState.AddModelError(string.Empty, "Unable to check availability. Please try again.");
        }

        return View(id.HasValue ? "Edit" : "Create", form);
    }

    [HttpGet]
    [Authorize(Roles = RoleNames.Borrower)]
    public IActionResult ReservationDetails(int id, string returnUrl = null)
    {
        returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        ViewData["ReservationReturnUrl"] = returnUrl;

        if (!int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var borrowerUserId))
            return Challenge();

        var reservation =
            _reservationService.GetReservationDetails(id, borrowerUserId);

        if (reservation == null)
            return NotFound();

        return View(reservation);
    }

    [HttpGet]
    [Authorize(Roles = RoleNames.Borrower)]
    public IActionResult Cancel(int id, string returnUrl = null)
    {
        returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        ViewData["ReservationReturnUrl"] = returnUrl;

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var borrowerUserId))
            return Challenge();

        var reservation = _reservationService.GetReservationDetails(id, borrowerUserId);

        if (reservation == null)
            return NotFound();

        if (reservation.Status != ReservationStatuses.Pending)
        {
            TempData["ReservationError"] = "Only pending reservations can be cancelled.";

            return RedirectToAction(nameof(ReservationDetails), new { id, returnUrl });
        }

        ViewData["Reservation"] = reservation;

        return View(new ReservationCancelFormModel
        {
            RowVersion = Convert.ToBase64String(reservation.RowVersion)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RoleNames.Borrower)]
    public IActionResult Cancel(int id, [Bind("Reason,RowVersion")] ReservationCancelFormModel model, string returnUrl = null)
    {
        returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        ViewData["ReservationReturnUrl"] = returnUrl;

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var borrowerUserId))
            return Challenge();

        var reservation = _reservationService.GetReservationDetails(id, borrowerUserId);

        if (reservation == null)
            return NotFound();

        if (reservation.Status != ReservationStatuses.Pending)
        {
            TempData["ReservationError"] = "Only pending reservations can be cancelled.";

            return RedirectToAction(nameof(ReservationDetails), new { id, returnUrl });
        }

        if (ModelState.IsValid)
        {
            try
            {
                var error = _reservationService.Cancel(id, model, borrowerUserId);

                if (error == null)
                {
                    TempData["ReservationSuccess"] = "Reservation cancelled.";

                    return RedirectToAction(nameof(ReservationDetails), new { id, returnUrl });
                }

                ModelState.AddModelError(string.Empty, error);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unable to cancel reservation {ReservationId}.", id);
                ModelState.AddModelError(string.Empty, "Unable to cancel your reservation. Reload it and try again.");
            }
        }

        ViewData["Reservation"] = reservation;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RoleNames.Borrower)]
    public IActionResult DiscardEdit(int id, string returnUrl = null)
    {
        returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        ViewData["ReservationReturnUrl"] = returnUrl;

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var borrowerUserId))
            return Challenge();

        var reservation = _reservationService.GetReservationDetails(id, borrowerUserId);

        if (reservation == null)
            return NotFound();

        HttpContext.Session.Remove($"ReservationDraft:Edit:{id}");
        HttpContext.Session.Remove($"SelectedEquipmentIds:Edit:{id}");

        return RedirectToAction(nameof(ReservationDetails), new { id, returnUrl });
    }
    
    [HttpGet]
    [Authorize(Roles = RoleNames.Borrower)]
    public IActionResult Submitted()
    {
        var number = TempData["ReservationNumber"] as string;
        if (number == null)
            return RedirectToAction("Index", "BrowseEquipment");
        return View(model: number);
    }

    #endregion

    #region CUSTODIAN & ADMIN Actions

    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Custodian},{RoleNames.Admin}")]
    public async Task<IActionResult> Index()
    {
        return await Pending(null);
    }

    // The status tabs: Pending (default), Approved, Borrowed, Rejected, All.
    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Custodian},{RoleNames.Admin}")]
    public async Task<IActionResult> Pending(string status = null)
    {
        try
        {
            var model = await _reservationService.GetReservationsAsync(status);
            return View("Pending", model);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load reservation requests.");
            TempData["ReservationLoadError"] = "Unable to load requests. Please try again.";
            return View("Pending", new ReservationListViewModel());
        }
    }

    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Custodian},{RoleNames.Admin}")]
    public IActionResult Review(int id)
    {
        try
        {
            var reservation = _reservationService.GetReservationDetails(id);
            if (reservation == null)
            {
                TempData["ReservationError"] = "That request was not found.";
                return RedirectToAction(nameof(Pending));
            }

            return View(reservation);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load reservation {ReservationId}.", id);
            TempData["ReservationError"] = "Unable to load the request. Please try again.";
            return RedirectToAction(nameof(Pending));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{RoleNames.Custodian},{RoleNames.Admin}")]
    public async Task<IActionResult> Approve(int id, string remarks)
    {
        ReservationDetailsViewModel reservation;
        try
        {
            reservation = _reservationService.GetReservationDetails(id);

            if (reservation == null)
                return NotFound();

            var (ok, message) = _reservationService.Approve(id, CurrentUserId(), CurrentName(), remarks);

            TempData[ok ? "ReservationSuccess" : "ReservationError"] = message;

            if (!ok)
                return RedirectToAction(nameof(Review), new { id });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to approve reservation {ReservationId}.", id);
            TempData["ReservationError"] = "Unable to approve the request. Please try again.";

            return RedirectToAction(nameof(Review), new { id });
        }

        // Approval has been saved successfully.
        await SendReservationEmailAsync(reservation.BorrowerEmail,
            $"HiRAM reservation {reservation.ReservationNumber} approved",
            ReservationEmailTemplates.ApprovalBody(
                reservation.ReservationNumber,
                reservation.StartDateTime,
                reservation.ExpectedReturnDateTime),
            "Reservation approved, but the approval email could not be sent.");

        return RedirectToAction(nameof(Review), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{RoleNames.Custodian},{RoleNames.Admin}")]
    public async Task<IActionResult> Reject(int id, string reason)
    {
        ReservationDetailsViewModel reservation;

        try
        {
            reservation = _reservationService.GetReservationDetails(id);

            if (reservation == null)
                return NotFound();

            var (ok, message) = _reservationService.Reject(
                id, CurrentUserId(), CurrentName(), reason);

            TempData[ok ? "ReservationSuccess" : "ReservationError"] = message;

            if (!ok)
                return RedirectToAction(nameof(Review), new { id });
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to reject reservation {ReservationId}.",
                id);

            TempData["ReservationError"] =
                "Unable to reject the request. Please try again.";

            return RedirectToAction(nameof(Review), new { id });
        }

        // Rejection has been saved successfully.
        await SendReservationEmailAsync(reservation.BorrowerEmail,
            $"HiRAM reservation {reservation.ReservationNumber} rejected",
            ReservationEmailTemplates.RejectionBody(
                reservation.ReservationNumber,
                reason),
            "Reservation rejected, but the rejection email could not be sent.");

        return RedirectToAction(nameof(Review), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{RoleNames.Custodian},{RoleNames.Admin}")]
    public async Task<IActionResult> Release(ReleaseReservationRequest request)
    {
        var id = request?.ReservationId ?? 0;

        try
        {
            var (ok, message) = _reservationService.Release(request, CurrentName());
            TempData[ok ? "ReservationSuccess" : "ReservationError"] = message;

            if (!ok)
                return RedirectToAction(nameof(Review), new { id });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to release reservation {ReservationId}.", id);
            TempData["ReservationError"] = "Unable to release the equipment. Please try again.";
            return RedirectToAction(nameof(Review), new { id });
        }

        // Release has been saved successfully.
        ReservationDetailsViewModel reservation;

        try
        {
            reservation = _reservationService.GetReservationDetails(id);

            if (reservation == null)
                throw new InvalidOperationException("The reservation details are missing.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Reservation {ReservationId} released, but email details could not be loaded.", id);
            TempData["ReservationEmailWarning"] = "Equipment released, but the release email could not be sent.";
            return RedirectToAction(nameof(Review), new { id });
        }

        await SendReservationEmailAsync(reservation.BorrowerEmail,
            $"HiRAM equipment released - {reservation.ReservationNumber}",
            ReservationEmailTemplates.ReleaseBody(
                reservation.ReservationNumber,
                reservation.BorrowedAt,
                reservation.ExpectedReturnDateTime),
            "Equipment released, but the release email could not be sent.");

        return RedirectToAction(nameof(Review), new { id });
    }

    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Custodian},{RoleNames.Admin}")]
    public async Task<IActionResult> Returns()
    {
        var model = await _reservationService.GetReservationsAsync(ReservationStatuses.Borrowed);
        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Custodian},{RoleNames.Admin}")]
    public IActionResult Return(int id)
    {
        var reservation = _reservationService.GetReservationDetails(id);

        if (reservation == null)
            return NotFound();

        if (reservation.Status != ReservationStatuses.Borrowed)
        {
            TempData["ReservationError"] = "Only borrowed reservations can be processed for return.";
            return RedirectToAction(nameof(Returns));
        }

        return View(reservation);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{RoleNames.Custodian},{RoleNames.Admin}")]
    public async Task<IActionResult> Return(ReturnReservationRequest request)
    {
        if (request == null)
            return BadRequest();

        if (!ModelState.IsValid)
            return ShowReturnForm(request, "Check the form. Inspection notes must be 500 characters or fewer.");

        try
        {
            var (ok, message) = _reservationService.Return(request, CurrentUserId(), CurrentName());

            if (!ok)
                return ShowReturnForm(request, message);

            TempData["ReservationSuccess"] = message;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to record return for reservation {ReservationId}.", request.ReservationId);
            TempData["ReservationError"] = "The return could not be saved. The reservation may have changed. " + "Review its current details before trying again.";
            return RedirectToAction(nameof(Review), new { id = request.ReservationId });
        }

        // Return has been saved successfully.
        ReservationDetailsViewModel reservation;

        try
        {
            reservation = _reservationService.GetReservationDetails(request.ReservationId);

            if (reservation == null)
                throw new InvalidOperationException("The reservation details are missing.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Return saved for reservation {ReservationId}, but email details could not be loaded.", request.ReservationId);

            TempData["ReservationEmailWarning"] = "The return was saved, but the return email could not be sent.";

            return RedirectToAction(
                nameof(Review), new { id = request.ReservationId });
        }

        await SendReservationEmailAsync(reservation.BorrowerEmail,
            $"HiRAM return recorded - {reservation.ReservationNumber}",
            ReservationEmailTemplates.ReturnBody(
                reservation.ReservationNumber,
                reservation.Items.Count(item => item.IsReturned),
                reservation.Items.Count),
            "The return was saved, but the return email could not be sent.");

        return RedirectToAction(nameof(Review), new { id = request.ReservationId });
    }

    #endregion

    #region Helpers

    private void PrepareEditView(int reservationId, ReservationEditFormModel model)
    {
        ViewData["ReservationId"] = reservationId;
        LoadEquipment(model);
    }

    private void LoadEquipment(ReservationFormModel model)
    {
        model.Equipment = new List<EquipmentListItem>();
        foreach (var id in (model.EquipmentIds ?? new List<int>()).Distinct())
        {
            var item = _equipmentService.GetBorrowableEquipmentById(id);
            if (item == null || item.Status == EquipmentAvailability.Unavailable)
            {
                ViewData["SelectionError"] = "Some selected equipment can no longer be borrowed. Return to Browse Equipment and review your selection.";
                continue;
            }
            model.Equipment.Add(item);
        }
    }

    private static List<int> ParseSelection(string value)
    {
        return (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }

    private IActionResult ShowReturnForm(ReturnReservationRequest request, string error)
    {
        var reservation = _reservationService.GetReservationDetails(request.ReservationId);

        if (reservation == null)
            return NotFound();
        
        if (reservation.Status != ReservationStatuses.Borrowed)
        {
            TempData["ReservationError"] = 
                "This reservation is no longer awaiting return.";

            return RedirectToAction(nameof(Review), new { id = request.ReservationId });
        }

        ViewData["ReturnError"] = error;
        ViewData["ReturnInput"] = request;

        return View("Return", reservation);
    }

    private void AddReservationErrors(ReservationValidationException exception)
    {
        foreach (var error in exception.Errors)
        {
            var fields = error.MemberNames.Any()
                ? error.MemberNames
                : new[] { string.Empty };

            foreach (var field in fields)
            {
                ModelState.AddModelError(field, error.ErrorMessage ?? "Invalid reservation input.");
            }
        }
    }

    private async Task SendReservationEmailAsync(string email, string subject, string body, string failureMessage)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new InvalidOperationException("The borrower email is missing.");

            await _emailManager.SendAsync(email, subject, body);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to send reservation email with subject {Subject}.", subject);
            TempData["ReservationEmailWarning"] = failureMessage;
        }
    }
    private int? CurrentUserId()
    {
        return int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : (int?)null;
    }

    private string CurrentName()
    {
        return User.Identity?.Name ?? "system";
    }

    #endregion
}
