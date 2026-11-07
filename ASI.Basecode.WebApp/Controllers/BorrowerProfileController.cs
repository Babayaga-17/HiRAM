// ASI.Basecode.WebApp/Controllers/BorrowerProfileController.cs
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

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize]
    public class BorrowerProfileController : ControllerBase<BorrowerProfileController>
    {
        private const int PageSize = 5;
        private readonly IBorrowerProfileService _borrowerProfileService;

        public BorrowerProfileController(IHttpContextAccessor httpContextAccessor,
                                        ILoggerFactory loggerFactory,
                                        IConfiguration configuration,
                                        IBorrowerProfileService borrowerProfileService,
                                        IMapper mapper = null) : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            this._borrowerProfileService = borrowerProfileService;
        }

        public IActionResult Index(string search, string status, int page = 1)
        {
            var vm = _borrowerProfileService.GetPage(search, status, page, PageSize);
            return View(vm);
        }

        public IActionResult History(int id)
        {
            var vm = _borrowerProfileService.GetHistory(id);
            if (vm == null) return NotFound();
            return PartialView("_History", vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Save(BorrowerFormModel model, string search, string status, int page = 1)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Check the borrower details and enter valid values.";
                return RedirectToAction(nameof(Index), new { search, status, page });
            }

            var (ok, message) = _borrowerProfileService.Save(model, User.Identity?.Name ?? "system");
            TempData[ok ? "Success" : "Error"] = message;
            return RedirectToAction(nameof(Index), new { search, status, page });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult SetStatus(int id, bool suspend, string reason, DateTime? until, string search, string status, int page = 1)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Check the status details and enter valid values.";
                return RedirectToAction(nameof(Index), new { search, status, page });
            }

            var (ok, message) = _borrowerProfileService.SetStatus(id, suspend, reason, until, User.Identity?.Name ?? "system");
            TempData[ok ? "Success" : "Error"] = message;
            return RedirectToAction(nameof(Index), new { search, status, page });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Delete(int id, string search, string status, int page = 1)
        {
            var (ok, message) = _borrowerProfileService.Delete(id);
            TempData[ok ? "Success" : "Error"] = message;
            return RedirectToAction(nameof(Index), new { search, status, page });
        }
    }
}
