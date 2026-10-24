using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASI.Basecode.WebApp.Controllers;

[Authorize(Roles = "Borrower,Custodian,Admin")]
public class DashboardController : Controller
{
    public IActionResult Index()
    {
        if (User.IsInRole("Borrower"))
            return View("Borrower");

        if (User.IsInRole("Custodian"))
            return View("Custodian");

        if (User.IsInRole("Admin"))
            return View("Admin");

        return Forbid();
    }
}