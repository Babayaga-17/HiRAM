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
using System.Collections.Generic;

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
        return Pending();
    }

    [HttpGet]
    public IActionResult Pending()
    {
        try
        {
            return View("Pending", _reservationService.GetPendingReservations());
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load pending reservation requests.");
            TempData["ReservationLoadError"] = "Unable to load pending requests. Please try again.";
            return View("Pending", new List<PendingReservationListItem>());
        }
    }

    [HttpGet]
    public IActionResult Details(int id)
    {
        try
        {
            var reservation = _reservationService.GetPendingReservationById(id);
            if (reservation == null)
            {
                TempData["ReservationError"] = "That pending request was not found. It may have already been reviewed.";
                return RedirectToAction(nameof(Pending));
            }

            return View(reservation);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load pending reservation {ReservationId}.", id);
            TempData["ReservationError"] = "Unable to load the request. Please try again.";
            return RedirectToAction(nameof(Pending));
        }
    }
}
