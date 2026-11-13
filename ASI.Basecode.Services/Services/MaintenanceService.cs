using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.Extensions.Logging;
namespace ASI.Basecode.Services.Services;
public class MaintenanceService : IMaintenanceService
{
    private readonly IMaintenanceRepository repository;
    private readonly IEmailManager email;
    private readonly ILogger<MaintenanceService> logger;
    public MaintenanceService(IMaintenanceRepository repository, IEmailManager email, ILogger<MaintenanceService> logger)
    { this.repository = repository; this.email = email; this.logger = logger; }
    public MaintenancePageViewModel GetPage(string search, string status, int page)
    {
        var query = repository.Records();
        search = search?.Trim();
        if (!string.IsNullOrEmpty(search)) query = query.Where(r => r.Equipment.EquipmentCode.Contains(search) || r.Equipment.EquipmentName.Contains(search));
        if (!string.IsNullOrEmpty(status)) query = query.Where(r => r.Status == status);
        var model = new MaintenancePageViewModel { Search = search, Status = status, TotalCount = query.Count(), RecordsAvailable = true };
        model.Page = Math.Clamp(page, 1, model.TotalPages);
        model.Records = query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.MaintenanceId).Skip((model.Page - 1) * 20).Take(20).ToList();
        return model;
    }
    private static readonly string TooFarMessage = $"Expected completion cannot be more than {MaintenanceForm.MaxMaintenanceDays} days from now.";
    public MaintenanceRecord GetDetails(int id) => repository.Records().FirstOrDefault(r => r.MaintenanceId == id);
    public async Task<(int Id, bool EmailSent)> ScheduleAsync(MaintenanceForm form, int userId)
    {
        Validator.ValidateObject(form, new ValidationContext(form), true);
        var record = new MaintenanceRecord();
        repository.InTransaction(() =>
        {
            var equipment = repository.FindEquipment(form.EquipmentId);
            if (equipment == null || !equipment.IsActive) throw new ValidationException("Select active equipment.");
            if (repository.HasMaintenanceConflict(form.EquipmentId)) throw new ValidationException("This equipment already has unfinished maintenance.");
            var conflicts = repository.ReservationConflicts(form.EquipmentId, form.StartDateTime);
            if (conflicts.Length > 0) throw new ValidationException("Resolve these reservations before scheduling: " + string.Join("; ", conflicts));
            record.EquipmentId = form.EquipmentId; record.MaintenanceType = form.MaintenanceType.Trim(); record.Description = form.Description.Trim();
            record.StartDateTime = form.StartDateTime; record.ExpectedCompletionDateTime = form.ExpectedCompletionDateTime;
            record.Status = MaintenanceStatuses.Scheduled; record.ReportedByUserId = userId;
            record.CreatedBy = userId.ToString(); record.CreatedAt = DateTime.Now;
            repository.Add(record);
        });
        return (record.MaintenanceId, await NotifyAsync(record, true));
    }
    public async Task<bool> UpdateAsync(MaintenanceUpdateForm form, int userId, bool isAdmin)
    {
        if (!isAdmin) throw new UnauthorizedAccessException();
        Validator.ValidateObject(form, new ValidationContext(form), true);
        MaintenanceRecord record = null;
        repository.InTransaction(() =>
        {
            record = repository.Find(form.Id);
            if (record == null) throw new ValidationException("Maintenance no longer exists.");
            var now = DateTime.Now;
            if (form.Operation == "start" && record.Status == MaintenanceStatuses.Scheduled)
            {
                if (record.StartDateTime > now) throw new ValidationException("Maintenance cannot begin before its scheduled start.");
                if (!form.ExpectedCompletionDateTime.HasValue || form.ExpectedCompletionDateTime <= now) throw new ValidationException("Provide an expected completion after the current time.");
                if (form.ExpectedCompletionDateTime > now.AddDays(MaintenanceForm.MaxMaintenanceDays)) throw new ValidationException(TooFarMessage);
                if (repository.HasReservationConflict(record.EquipmentId, record.StartDateTime)) throw new ValidationException("Resolve conflicting reservations before proceeding.");
                record.ExpectedCompletionDateTime = form.ExpectedCompletionDateTime;
                record.Status = MaintenanceStatuses.InProgress;
            }
            else if (form.Operation == "estimate" && record.Status == MaintenanceStatuses.InProgress)
            {
                if (!form.ExpectedCompletionDateTime.HasValue || form.ExpectedCompletionDateTime <= now) throw new ValidationException("Provide a future expected completion.");
                if (form.ExpectedCompletionDateTime > now.AddDays(MaintenanceForm.MaxMaintenanceDays)) throw new ValidationException(TooFarMessage);
                record.ExpectedCompletionDateTime = form.ExpectedCompletionDateTime;
            }
            else if (form.Operation == "complete" && record.Status == MaintenanceStatuses.InProgress)
            {
                if (!record.Equipment.IsActive || record.Equipment.ConditionStatus == EquipmentConditions.Damaged || record.Equipment.ConditionStatus == EquipmentConditions.Lost)
                    throw new ValidationException("Update equipment to an active, operational condition before completing maintenance.");
                record.Status = MaintenanceStatuses.Completed; record.EndDateTime = now; record.CompletedByUserId = userId;
            }
            else if (form.Operation == "cancel" && record.Status == MaintenanceStatuses.Scheduled) record.Status = MaintenanceStatuses.Cancelled;
            else throw new ValidationException("This action is unavailable for the current maintenance status.");
            record.Remarks = form.Remarks?.Trim(); record.UpdatedBy = userId.ToString(); record.UpdatedAt = now;
        });
        return await NotifyAsync(record, false);
    }
    private async Task<bool> NotifyAsync(MaintenanceRecord record, bool scheduled)
    {
        try
        {
            var recipients = scheduled ? repository.AdminEmails() : new[] { repository.ReporterEmail(record.ReportedByUserId) };
            var sent = true;
            if (recipients.Length == 0) return false;
            foreach (var recipient in recipients)
            {
                if (string.IsNullOrWhiteSpace(recipient)) { sent = false; continue; }
                try { await email.SendAsync(recipient, $"HiRAM maintenance #{record.MaintenanceId}: {record.Status}", $"Maintenance #{record.MaintenanceId} for equipment ID {record.EquipmentId}.\nStatus: {record.Status}\nWork: {record.Description}\nStart: {record.StartDateTime:g}\nExpected completion: {record.ExpectedCompletionDateTime:g}\nActual completion: {record.EndDateTime:g}\nRemarks: {record.Remarks}"); }
                catch (Exception exception) { logger.LogWarning(exception, "Maintenance {MaintenanceId} notification failed.", record.MaintenanceId); sent = false; }
            }
            return sent;
        }
        catch (Exception exception) { logger.LogWarning(exception, "Maintenance notification recipients could not be loaded."); return false; }
    }
}

