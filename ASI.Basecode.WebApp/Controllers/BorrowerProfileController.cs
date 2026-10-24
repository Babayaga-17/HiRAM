// ASI.Basecode.WebApp/Controllers/BorrowerProfileController.cs
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize]
    public class BorrowerProfileController : Controller
    {
        private const int PageSize = 5;
        private readonly IBorrowerProfileService _service;
        public BorrowerProfileController(IBorrowerProfileService service) => _service = service;

        public IActionResult Index(string search, string status, int page = 1)
        {
            var vm = _service.GetPage(search, status, page, PageSize);
            vm.Departments = _service.GetDepartments();
            return View(vm);
        }

        public IActionResult History(int id)
        {
            var vm = _service.GetHistory(id);
            if (vm == null) return NotFound();
            return PartialView("_History", vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Save(BorrowerFormModel model, string search, string status, int page = 1)
        {
            var (ok, message) = _service.Save(model, User.Identity?.Name ?? "system");
            TempData[ok ? "Success" : "Error"] = message;
            return RedirectToAction(nameof(Index), new { search, status, page });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult SetStatus(int id, bool suspend, string reason, DateTime? until, string search, string status, int page = 1)
        {
            var (ok, message) = _service.SetStatus(id, suspend, reason, until, User.Identity?.Name ?? "system");
            TempData[ok ? "Success" : "Error"] = message;
            return RedirectToAction(nameof(Index), new { search, status, page });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Delete(int id, string search, string status, int page = 1)
        {
            var (ok, message) = _service.Delete(id);
            TempData[ok ? "Success" : "Error"] = message;
            return RedirectToAction(nameof(Index), new { search, status, page });
        }
    }
}
