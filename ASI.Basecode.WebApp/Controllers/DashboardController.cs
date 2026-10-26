using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace ASI.Basecode.WebApp.Controllers;

[Authorize(Roles = "Borrower,Custodian,Admin")]
public class DashboardController : Controller
{
    private readonly IReservationService _reservationService;

    public DashboardController(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    public IActionResult Index()
    {
        if (User.IsInRole("Borrower"))
            return View("Borrower");

        if (User.IsInRole("Custodian"))
        {
            List<PendingReservationListItem> pending;
            try
            {
                pending = _reservationService.GetPendingReservations();
            }
            catch
            {
                pending = new List<PendingReservationListItem>();
            }

            return View("Custodian", pending);
        }

        if (User.IsInRole("Admin"))
        {
            try
            {
                ViewBag.PendingCount = _reservationService.CountPendingReservations();
            }
            catch
            {
                ViewBag.PendingCount = null;
            }

            return View("Admin");
        }

        return Forbid();
    }
}
