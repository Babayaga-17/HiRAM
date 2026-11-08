using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace ASI.Basecode.WebApp.Controllers;

[Authorize(Roles = $"{RoleNames.Borrower}, {RoleNames.Custodian}, {RoleNames.Admin}")]
public class DashboardController : Controller
{
    private readonly IReservationService _reservationService;
    private readonly IAdminDashboardService _adminDashboardService;

    public DashboardController(IReservationService reservationService, IAdminDashboardService adminDashboardService)
    {
        _reservationService = reservationService;
        _adminDashboardService = adminDashboardService;
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
            return View("Admin", _adminDashboardService.GetOverview());
        }

        return Forbid();
    }
}
