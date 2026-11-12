using ASI.Basecode.Resources.Constants;
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
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers;

[Authorize(Roles = $"{RoleNames.Borrower}, {RoleNames.Custodian}, {RoleNames.Admin}")]
public class DashboardController : ControllerBase<DashboardController>
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IHttpContextAccessor httpContextAccessor,
                                ILoggerFactory loggerFactory,
                                IConfiguration configuration,
                                IDashboardService dashboardService,
                                IMapper mapper = null) : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        string viewName = null;

        try
        {
            if (User.IsInRole(RoleNames.Borrower))
            {
                viewName = RoleNames.Borrower;
                if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var borrowerUserId) || borrowerUserId <= 0)
                    return Challenge();
                var model = await _dashboardService.GetBorrowerOverviewAsync(borrowerUserId);

                return View(viewName, model);
            }

            if (User.IsInRole(RoleNames.Custodian))
            {
                viewName = RoleNames.Custodian;
                var model = await _dashboardService.GetCustodianOverviewAsync();

                return View(viewName, model);
            }

            if (User.IsInRole(RoleNames.Admin))
            {
                viewName = RoleNames.Admin;
                var model = await _dashboardService.GetAdminOverviewAsync();

                return View(viewName, model);
            }

            return Forbid();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception, "Unable to load the {Dashboard} dashboard.", viewName);

            ViewData["DashboardError"] = "Your dashboard could not be loaded. Refresh to try again.";

            return View(viewName);
        }
    }
}
