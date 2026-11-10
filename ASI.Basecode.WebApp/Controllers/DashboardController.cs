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
        if (User.IsInRole("Borrower"))
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var borrowerUserId) || borrowerUserId <= 0)
                return Challenge();

            var borrowerOverview = await _dashboardService.GetBorrowerOverviewAsync(borrowerUserId);
            return View("Borrower", borrowerOverview);
        }

        if (User.IsInRole("Custodian"))
        {
            return View("Custodian", await Task.FromResult(_dashboardService.GetCustodianOverview()));
        }

        if (User.IsInRole("Admin"))
        {
            return View("Admin", await Task.FromResult(_dashboardService.GetAdminOverview()));
        }

        return Forbid();
    }
}
