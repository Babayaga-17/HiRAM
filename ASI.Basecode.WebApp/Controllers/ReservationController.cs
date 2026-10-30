// ASI.Basecode.WebApp/Controllers/ReservationController.cs
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;

namespace ASI.Basecode.WebApp.Controllers;

[Authorize(Roles = "Custodian, Admin")]
public class ReservationController : ControllerBase<ReservationController>
{
    private readonly IReservationService _reservationService;

    public ReservationController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IReservationService reservationService,
        IMapper mapper = null) : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _reservationService = reservationService;
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

    private int? CurrentUserId()
    {
        return int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : (int?)null;
    }

    private string CurrentName()
    {
        return User.Identity?.Name ?? "system";
    }
}
