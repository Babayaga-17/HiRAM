using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
namespace ASI.Basecode.WebApp.Controllers;

[Authorize(Roles = $"{RoleNames.Custodian}, {RoleNames.Admin}")]
public class MaintenanceController : Controller
{
    private readonly IMaintenanceService maintenance;
    private readonly IEquipmentService equipment;
    private readonly ILogger<MaintenanceController> logger;
    public MaintenanceController(IMaintenanceService maintenance, IEquipmentService equipment, ILogger<MaintenanceController> logger)
    { this.maintenance = maintenance; this.equipment = equipment; this.logger = logger; }
    [HttpGet]
    public IActionResult Index(string search, string status, int page = 1, int? equipmentId = null)
    {
        if (equipmentId.HasValue) return RedirectToAction(nameof(Create), new { equipmentId });
        try { return View(maintenance.GetPage(search, status, page)); }
        catch (Exception exception) { logger.LogError(exception, "Unable to load maintenance."); TempData["MaintenanceError"] = "Maintenance could not be loaded. Check database setup and try again."; return View(new MaintenancePageViewModel()); }
    }
    [HttpGet]
    public IActionResult Create(int? equipmentId) => View(CreateModel(new MaintenanceForm { EquipmentId = equipmentId ?? 0 }));
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MaintenanceForm form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var result = await maintenance.ScheduleAsync(form, ActorId());
                TempData["MaintenanceSuccess"] = result.EmailSent ? "Maintenance scheduled and email submitted." : "Maintenance scheduled, but email notification failed.";
                return RedirectToAction(nameof(Details), new { id = result.Id });
            }
            catch (ValidationException exception) { ModelState.AddModelError("", exception.Message); }
            catch (Exception exception) { logger.LogError(exception, "Unable to schedule maintenance."); ModelState.AddModelError("", "Unable to schedule maintenance. Please try again."); }
        }
        return View(CreateModel(form));
    }
    [HttpGet]
    public IActionResult Details(int id, string search, string status, int page = 1)
    {
        ViewData["ListSearch"] = search; ViewData["ListStatus"] = status; ViewData["ListPage"] = page;
        var record = maintenance.GetDetails(id);
        if (record == null) return NotFound();
        return View(record);
    }
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(MaintenanceUpdateForm form, string search, string status, int page = 1)
    {
        if (!ModelState.IsValid)
        {
            TempData["MaintenanceError"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Details), new { id = form.Id, search, status, page });
        }
        try
        {
            var emailSent = await maintenance.UpdateAsync(form, ActorId(), User.IsInRole("Admin"));
            TempData["MaintenanceSuccess"] = emailSent ? "Maintenance updated and email submitted." : "Maintenance updated, but email notification failed.";
        }
        catch (ValidationException exception) { TempData["MaintenanceError"] = exception.Message; }
        catch (Exception exception) { logger.LogError(exception, "Unable to update maintenance."); TempData["MaintenanceError"] = "Unable to update maintenance. Please try again."; }
        return RedirectToAction(nameof(Details), new { id = form.Id, search, status, page });
    }
    private int ActorId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
    private MaintenancePageViewModel CreateModel(MaintenanceForm form)
    {
        var model = new MaintenancePageViewModel { Form = form, SelectedEquipmentId = form.EquipmentId };
        try { model.Equipment = equipment.GetEquipments().Where(e => e.IsActive).ToList(); }
        catch (Exception exception) { logger.LogError(exception, "Unable to load maintenance equipment."); model.EquipmentError = "Equipment could not be loaded. Please try again."; }
        return model;
    }
}
