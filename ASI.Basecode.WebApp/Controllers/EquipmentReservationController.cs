using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.ServiceModels.Reservation;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ASI.Basecode.WebApp.Controllers;

[Authorize(Roles = "Borrower")]
public class EquipmentReservationController : ControllerBase<EquipmentReservationController>
{
    private const string SelectionKey = "SelectedEquipmentIds";

    private readonly IEquipmentService _equipmentService;
    private readonly IEquipmentReservationService _reservationService;

    public EquipmentReservationController(
                            IHttpContextAccessor httpContextAccessor, 
                            ILoggerFactory loggerFactory, 
                            IConfiguration configuration,
                            IEquipmentService equipmentService,
                            IEquipmentReservationService reservationService,
                            IMapper mapper = null) : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _equipmentService = equipmentService;
        _reservationService = reservationService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var borrowerUserId))
        {
            return Challenge();
        }

        var reservations = _reservationService.GetPendingReservations(borrowerUserId);

        return View(reservations);
    }

    // Static samples for the details UI; no reservation records are loaded.
    [HttpGet]
    public IActionResult Details(int id)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var borrowerUserId))
        {
            return Challenge();
        }

        var reservation = _reservationService.GetReservationDetails(id, borrowerUserId);

        if (reservation == null)
            return NotFound();

        return View(reservation);
    }

    [HttpGet]
    public IActionResult Create()
    {
        var draft = HttpContext.Session.GetString(
            "ReservationDraft:Create");

        var model = draft == null
            ? new ReservationFormModel()
            : JsonSerializer.Deserialize<ReservationFormModel>(draft);

        model.EquipmentIds = ParseSelection(
            HttpContext.Session.GetString(SelectionKey));

        LoadEquipment(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create([Bind("EquipmentIds,Pickup,ExpectedReturn,Purpose")] ReservationFormModel model)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var borrowerUserId))
            return Challenge();

        if (ModelState.IsValid)
        {
            try
            {
                var result = _reservationService.Submit(model, borrowerUserId);
                if (result.Error == null)
                {
                    HttpContext.Session.Remove(SelectionKey);
                    HttpContext.Session.Remove("ReservationDraft:Create");
                    TempData["ReservationNumber"] = result.ReservationNumber;
                    return RedirectToAction(nameof(Submitted));
                }
                ModelState.AddModelError(string.Empty, result.Error);
            }
            catch (Exception)
            {
                //_logger.LogError(exception, "Unable to submit equipment reservation for borrower {UserId}.", borrowerUserId);
                ModelState.AddModelError(string.Empty,
                    "Your reservation could not be completed. Please try again; availability will be checked again.");
            }
        }

        LoadEquipment(model);
        return View(model);
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        if (!int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var borrowerUserId))
            return Challenge();

        var reservation =
            _reservationService.GetReservationDetails(id, borrowerUserId);

        if (reservation == null)
            return NotFound();

        if (reservation.Status != "Pending")
        {
            TempData["ReservationError"] =
                "Only pending reservations can be edited.";

            return RedirectToAction(nameof(Details), new { id });
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
            HttpContext.Session.SetString(
                selectionKey,
                string.Join(",", model.EquipmentIds));
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
    public IActionResult Edit(int id, [Bind("EquipmentIds,Pickup,ExpectedReturn,Purpose,RowVersion")] ReservationEditFormModel model)
    {
        if (!int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var borrowerUserId))
            return Challenge();

        var reservation =
            _reservationService.GetReservationDetails(id, borrowerUserId);

        if (reservation == null)
            return NotFound();

        if (reservation.Status != "Pending")
        {
            TempData["ReservationError"] =
                "Only pending reservations can be edited.";

            return RedirectToAction(nameof(Details), new { id });
        }

        if (ModelState.IsValid)
        {
            try
            {
                var error =
                    _reservationService.Edit(id, model, borrowerUserId);

                if (error == null)
                {
                    HttpContext.Session.Remove($"ReservationDraft:Edit:{id}");
                    HttpContext.Session.Remove($"SelectedEquipmentIds:Edit:{id}");

                    TempData["ReservationSuccess"] = "Reservation updated.";

                    return RedirectToAction(nameof(Details), new { id });
                }

                ModelState.AddModelError(string.Empty, error);
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

    [HttpGet]
    public IActionResult Cancel(int id)
    {
        if (!int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var borrowerUserId))
            return Challenge();

        var reservation =
            _reservationService.GetReservationDetails(id, borrowerUserId);

        if (reservation == null)
            return NotFound();

        if (reservation.Status != "Pending")
        {
            TempData["ReservationError"] =
                "Only pending reservations can be cancelled.";

            return RedirectToAction(nameof(Details), new { id });
        }

        ViewData["Reservation"] = reservation;

        return View(new ReservationCancelFormModel
        {
            RowVersion = Convert.ToBase64String(reservation.RowVersion)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Cancel(int id, [Bind("Reason,RowVersion")] ReservationCancelFormModel model)
    {
        if (!int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var borrowerUserId))
            return Challenge();

        var reservation =
            _reservationService.GetReservationDetails(id, borrowerUserId);

        if (reservation == null)
            return NotFound();

        if (reservation.Status != "Pending")
        {
            TempData["ReservationError"] =
                "Only pending reservations can be cancelled.";

            return RedirectToAction(nameof(Details), new { id });
        }

        if (ModelState.IsValid)
        {
            try
            {
                var error =
                    _reservationService.Cancel(id, model, borrowerUserId);

                if (error == null)
                {
                    TempData["ReservationSuccess"] =
                        "Reservation cancelled.";

                    return RedirectToAction(nameof(Details), new { id });
                }

                ModelState.AddModelError(string.Empty, error);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception,
                    "Unable to cancel reservation {ReservationId}.", id);

                ModelState.AddModelError(string.Empty,
                    "Unable to cancel your reservation. Reload it and try again.");
            }
        }

        ViewData["Reservation"] = reservation;
        return View(model);
    }

    private void PrepareEditView(int reservationId, ReservationEditFormModel model)
    {
        ViewData["ReservationId"] = reservationId;
        LoadEquipment(model);
    }

    [HttpGet]
    public IActionResult Submitted()
    {
        var number = TempData["ReservationNumber"] as string;
        if (number == null) return RedirectToAction("Index", "BrowseEquipment");
        return View(model: number);
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ChangeEquipment(
    int? id,
    [Bind("EquipmentIds,Pickup,ExpectedReturn,Purpose,RowVersion")]
    ReservationEditFormModel model)
    {
        if (!int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var borrowerUserId))
            return Challenge();

        if (id.HasValue)
        {
            var reservation = _reservationService
                .GetReservationDetails(id.Value, borrowerUserId);

            if (reservation == null)
                return NotFound();

            if (reservation.Status != "Pending")
            {
                TempData["ReservationError"] =
                    "Only pending reservations can be edited.";

                return RedirectToAction(
                    nameof(Details), new { id = id.Value });
            }

            HttpContext.Session.SetString(
                $"ReservationDraft:Edit:{id.Value}",
                JsonSerializer.Serialize(model));

            HttpContext.Session.SetString(
                $"SelectedEquipmentIds:Edit:{id.Value}",
                string.Join(",", model.EquipmentIds));
        }
        else
        {
            HttpContext.Session.SetString(
                "ReservationDraft:Create",
                JsonSerializer.Serialize<ReservationFormModel>(model));

            HttpContext.Session.SetString(
                SelectionKey,
                string.Join(",", model.EquipmentIds));
        }

        return RedirectToAction(
            "Index",
            "BrowseEquipment",
            new { reservationId = id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DiscardEdit(int id)
    {
        if (!int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var borrowerUserId))
            return Challenge();

        var reservation =
            _reservationService.GetReservationDetails(id, borrowerUserId);

        if (reservation == null)
            return NotFound();

        HttpContext.Session.Remove($"ReservationDraft:Edit:{id}");
        HttpContext.Session.Remove($"SelectedEquipmentIds:Edit:{id}");

        return RedirectToAction(nameof(Details), new { id });
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
}
