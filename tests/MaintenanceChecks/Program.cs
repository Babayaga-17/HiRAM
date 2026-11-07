using System.ComponentModel.DataAnnotations;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Microsoft.Extensions.Logging.Abstractions;

int passed = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
async Task Reject(Func<Task> action, string name) { try { await action(); } catch (ValidationException) { passed++; Console.WriteLine("PASS " + name); return; } throw new Exception(name); }
var repo = new MemoryRepository();
var email = new TestEmail(repo);
var service = new MaintenanceService(repo, email, NullLogger<MaintenanceService>.Instance);
MaintenanceForm Form() => new() { EquipmentId = 1, MaintenanceType = "Repair", Description = "Inspect power supply", StartDateTime = DateTime.Now.AddMinutes(-5) };
repo.Conflict = true;
await Reject(async () => { await service.ScheduleAsync(Form(), 1); }, "Future reservation blocks scheduling");
Check(repo.Data.Count == 0 && email.Count == 0, "Rejected schedule has no record or email");
repo.Conflict = false;
var bad = Form(); bad.ExpectedCompletionDateTime = bad.StartDateTime.AddMinutes(-1);
await Reject(async () => { await service.ScheduleAsync(bad, 1); }, "Estimate must follow start");
var scheduled = await service.ScheduleAsync(Form(), 1);
var record = repo.Data.Single();
Check(record.Status == "Scheduled" && record.EndDateTime == null, "Schedule without estimate keeps actual end empty");
Check(email.Count == 1 && !email.SentInsideTransaction, "Email runs after committed save");
await Reject(async () => { await service.ScheduleAsync(Form(), 1); }, "Duplicate unfinished maintenance rejected");
try { await service.UpdateAsync(new() { Id = 1, Operation = "start" }, 1, false); throw new Exception("Permission check failed"); } catch (UnauthorizedAccessException) { passed++; }
await Reject(async () => { await service.UpdateAsync(new() { Id = 1, Operation = "start" }, 1, true); }, "Admin start requires estimate");
await service.UpdateAsync(new() { Id = 1, Operation = "start", ExpectedCompletionDateTime = DateTime.Now.AddDays(1) }, 1, true);
Check(record.Status == "InProgress" && record.EndDateTime == null, "Proceed preserves actual end");
await Reject(async () => { await service.UpdateAsync(new() { Id = 1, Operation = "cancel" }, 1, true); }, "In-progress work cannot be cancelled as a schedule");
repo.Item.ConditionStatus = "Damaged";
await Reject(async () => { await service.UpdateAsync(new() { Id = 1, Operation = "complete" }, 1, true); }, "Damaged equipment cannot be confirmed operational");
repo.Item.ConditionStatus = "Good"; email.Fail = true;
var notified = await service.UpdateAsync(new() { Id = 1, Operation = "complete" }, 1, true);
Check(!notified && record.Status == "Completed" && record.EndDateTime.HasValue && record.CompletedByUserId == 1, "Email failure preserves completion and actual end");
Check(service.GetPage(null, null, 99).Page == 1, "Pagination clamps invalid page");
Console.WriteLine($"{passed} maintenance checks passed.");

class MemoryRepository : IMaintenanceRepository
{
    public List<MaintenanceRecord> Data = new();
    public Equipment Item = new() { EquipmentId = 1, IsActive = true, ConditionStatus = "Good" };
    public bool Conflict, Inside;
    public IQueryable<MaintenanceRecord> Records() => Data.AsQueryable();
    public MaintenanceRecord Find(int id) => Data.SingleOrDefault(r => r.MaintenanceId == id);
    public Equipment FindEquipment(int id) => id == 1 ? Item : null;
    public string[] ReservationConflicts(int id, DateTime start) => Conflict ? new[] { "RES-TEST" } : Array.Empty<string>();
    public bool HasReservationConflict(int id, DateTime start) => Conflict;
    public bool HasMaintenanceConflict(int id, int? excludeId = null) => Data.Any(r => r.Status == "Scheduled" || r.Status == "InProgress");
    public string[] AdminEmails() => new[] { "admin@example.test" };
    public string ReporterEmail(int? id) => "custodian@example.test";
    public void Add(MaintenanceRecord record) { record.MaintenanceId = Data.Count + 1; record.Equipment = Item; Data.Add(record); }
    public void Save() { }
    public void InTransaction(Action action) { Inside = true; try { action(); } finally { Inside = false; } }
}
class TestEmail(MemoryRepository repo) : IEmailManager
{
    public int Count;
    public bool Fail, SentInsideTransaction;
    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        Count++; SentInsideTransaction |= repo.Inside;
        if (Fail) throw new Exception("Simulated email failure");
        return Task.CompletedTask;
    }
}
