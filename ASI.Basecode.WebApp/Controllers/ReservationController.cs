// ASI.Basecode.WebApp/Controllers/ReservationController.cs
using ASI.Basecode.Resources.Constants;
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
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers;

[Authorize(Roles = $"{RoleNames.Borrower}, {RoleNames.Custodian}, {RoleNames.Admin}")]
public class ReservationController : ControllerBase<ReservationController>
{
    private readonly IReservationService _reservationService;

    public ReservationController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IReservationService reservationService,
        IEquipmentReservationService equipmentReservationService,   
        IMapper mapper = null) : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _reservationService = reservationService;
    }

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
    public IActionResult Index()
    {
        return Pending(null);
    }

    // The status tabs: Pending (default), Approved, Borrowed, Rejected, All.
    [HttpGet]
    public IActionResult Pending(string status = null)
    {
        try
        {
            return View("Pending", _reservationService.GetReservations(status));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load reservation requests.");
            TempData["ReservationLoadError"] = "Unable to load requests. Please try again.";
            return View("Pending", new ReservationListViewModel());
        }
    }

    [HttpGet]
    public IActionResult Details(int id)
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

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Approve(int id, string remarks)
    {
        try
        {
            var (ok, message) = _reservationService.Approve(id, CurrentUserId(), CurrentName(), remarks);
            TempData[ok ? "ReservationSuccess" : "ReservationError"] = message;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to approve reservation {ReservationId}.", id);
            TempData["ReservationError"] = "Unable to approve the request. Please try again.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Reject(int id, string reason)
    {
        try
        {
            var (ok, message) = _reservationService.Reject(id, CurrentUserId(), CurrentName(), reason);
            TempData[ok ? "ReservationSuccess" : "ReservationError"] = message;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to reject reservation {ReservationId}.", id);
            TempData["ReservationError"] = "Unable to reject the request. Please try again.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Release(ReleaseReservationRequest request)
    {
        var id = request?.ReservationId ?? 0;
        try
        {
            var (ok, message) = _reservationService.Release(request, CurrentName());
            TempData[ok ? "ReservationSuccess" : "ReservationError"] = message;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to release reservation {ReservationId}.", id);
            TempData["ReservationError"] = "Unable to release the equipment. Please try again.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public IActionResult Returns()
    {
        var model = _reservationService.GetReservations(ReservationStatuses.Borrowed);
        return View(model);
    }

    [HttpGet]
    public IActionResult Return(int id)
    {
        var reservation = _reservationService.GetReservationDetails(id);

        if (reservation == null)
            return NotFound();

        if (reservation.Status != ReservationStatuses.Borrowed)
        {
            TempData["ReservationError"] =
                "Only borrowed reservations can be processed for return.";

            return RedirectToAction(nameof(Returns));
        }

        return View(reservation);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Return(ReturnReservationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ShowReturnForm(
                request,
                "Check the form. Inspection notes must be 500 characters or fewer.");
        }

        try
        {
            var (ok, message) = _reservationService.Return(
                request,
                CurrentUserId(),
                CurrentName());

            if (!ok)
                return ShowReturnForm(request, message);

            TempData["ReservationSuccess"] = message;

            return RedirectToAction(
                nameof(Details),
                new { id = request.ReservationId });
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to record return for reservation {ReservationId}.",
                request.ReservationId);

            TempData["ReservationError"] =
                "The return could not be saved. The reservation may have changed. "
                + "Review its current details before trying again.";

            return RedirectToAction(
                nameof(Details),
                new { id = request.ReservationId });
        }
    }

    private IActionResult ShowReturnForm(
        ReturnReservationRequest request,
        string error)
    {
        var reservation =
            _reservationService.GetReservationDetails(request.ReservationId);

        if (reservation == null)
            return NotFound();

        if (reservation.Status != ReservationStatuses.Borrowed)
        {
            TempData["ReservationError"] =
                "This reservation is no longer awaiting return.";

            return RedirectToAction(
                nameof(Details),
                new { id = request.ReservationId });
        }

        ViewData["ReturnError"] = error;
        ViewData["ReturnInput"] = request;

        return View("Return", reservation);
    }

    private int? CurrentUserId()
    {
        return int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : (int?)null;
    }

    private string CurrentName()
    {
        return User.Identity?.Name ?? "system";
    }
}
