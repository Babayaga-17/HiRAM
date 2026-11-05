using System;
using System.Data;
using System.Linq;
using System.Security.Claims;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace ASI.Basecode.WebApp.Controllers;
[Authorize(Roles = "Custodian,Admin")]
public class MaintenanceController : Controller
{
    private readonly HiramDbContext db;
    public MaintenanceController(HiramDbContext db) { this.db = db; }
    public IActionResult Index(int? equipmentId)
    {
        ViewBag.SelectedEquipmentId = equipmentId;
        ViewBag.Equipment = db.Equipments.AsNoTracking().Where(e => e.IsActive).OrderBy(e => e.EquipmentName).ToList();
        return View(db.MaintenanceRecords.AsNoTracking().Include(r => r.Equipment).OrderByDescending(r => r.CreatedAt).ToList());
    }
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Schedule(MaintenanceForm model)
    {
        using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
        if (model.StartDateTime == default || (model.EndDateTime.HasValue && model.EndDateTime <= model.StartDateTime))
            ModelState.AddModelError("", "Enter a valid maintenance period with the end after the start.");
        var end = model.EndDateTime ?? DateTime.MaxValue;
        if (!db.Equipments.Any(e => e.EquipmentId == model.EquipmentId && e.IsActive))
            ModelState.AddModelError("", "Select active equipment.");
        if (db.MaintenanceRecords.Any(r => r.EquipmentId == model.EquipmentId && (r.Status == MaintenanceStatuses.Scheduled || r.Status == MaintenanceStatuses.InProgress) && r.StartDateTime < end && (r.EndDateTime == null || r.EndDateTime > model.StartDateTime)) ||
            db.ReservationItems.Any(r => r.EquipmentId == model.EquipmentId && !r.IsReturned && ReservationStatuses.Blocking.Contains(r.Reservation.Status) && r.Reservation.StartDateTime < end && r.Reservation.ExpectedReturnDateTime > model.StartDateTime))
            ModelState.AddModelError("", "This period overlaps a reservation or existing maintenance.");
        if (!ModelState.IsValid) { TempData["MaintenanceError"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)); return RedirectToAction(nameof(Index)); }
        db.MaintenanceRecords.Add(new MaintenanceRecord { EquipmentId = model.EquipmentId, MaintenanceType = model.MaintenanceType.Trim(), Description = model.Description.Trim(), StartDateTime = model.StartDateTime, EndDateTime = model.EndDateTime, Status = MaintenanceStatuses.Scheduled, ReportedByUserId = ActorId(), CreatedBy = User.Identity.Name ?? "Admin", CreatedAt = DateTime.Now });
        db.SaveChanges(); transaction.Commit();
        TempData["MaintenanceSuccess"] = "Maintenance scheduled.";
        return RedirectToAction(nameof(Index));
    }
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public IActionResult Proceed(int id, string operation, string remarks)
    {
        using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
        var record = db.MaintenanceRecords.SingleOrDefault(r => r.MaintenanceId == id);
        if (record == null) return NotFound();
        if ((remarks?.Length ?? 0) > 500) { TempData["MaintenanceError"] = "Remarks must be 500 characters or fewer."; return RedirectToAction(nameof(Index)); }
        if (operation == "start" && record.Status == MaintenanceStatuses.Scheduled && record.StartDateTime <= DateTime.Now && (record.EndDateTime == null || record.EndDateTime > DateTime.Now)) record.Status = MaintenanceStatuses.InProgress;
        else if (operation == "complete" && record.Status == MaintenanceStatuses.InProgress) { record.Status = MaintenanceStatuses.Completed; record.EndDateTime = DateTime.Now; record.CompletedByUserId = ActorId(); }
        else if (operation == "cancel" && record.Status == MaintenanceStatuses.Scheduled) record.Status = MaintenanceStatuses.Cancelled;
        else { TempData["MaintenanceError"] = "This action is unavailable for the current maintenance status or dates."; return RedirectToAction(nameof(Index)); }
        record.Remarks = remarks?.Trim(); record.UpdatedBy = User.Identity.Name ?? "Admin"; record.UpdatedAt = DateTime.Now;
        db.SaveChanges(); transaction.Commit(); TempData["MaintenanceSuccess"] = "Maintenance updated.";
        return RedirectToAction(nameof(Index));
    }
    private int? ActorId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
