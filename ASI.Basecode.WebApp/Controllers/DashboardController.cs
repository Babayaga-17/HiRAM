using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ASI.Basecode.WebApp.Controllers
{
    public class DashboardController : ControllerBase<AccountController>
    {
        public DashboardController(
                                IHttpContextAccessor httpContextAccessor, 
                                ILoggerFactory loggerFactory, 
                                IConfiguration configuration, 
                                IMapper mapper = null) : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {

        }

        public IActionResult Index()
        {
            if (User.IsInRole("Borrower"))
            {
                return View("Borrower");
            }

            if (User.IsInRole("Custodian"))
            {
                return View("Custodian");
            }

            if (User.IsInRole("Admin"))
            {
                return View("Admin");
            }

            return Forbid();
        }
    }
}
