using System.Reflection;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Microsoft.Extensions.Logging.Abstractions;

static T Stub<T>(Func<string, object[], object> read) where T : class
{
    var result = DispatchProxy.Create<T, ReadProxy>();
    ((ReadProxy)(object)result).Read = read;
    return result;
}
static void Check(bool passed, string message) { if (!passed) throw new Exception(message); }
var failAccounts = false;
var empty = false;
var accounts = Stub<IUserAccountService>((name, args) => {
    Check(name == "GetUserAccounts", "Unexpected account mutation");
    if (failAccounts) throw new InvalidOperationException("Unavailable");
    return empty ? new List<UserAccountListItem>() : Enumerable.Range(1, 6).Select(i => new UserAccountListItem { UserId = i, AccountStatus = i == 6 ? "Active" : AccountStatuses.PendingApproval }).ToList();
});
var equipment = Stub<IEquipmentService>((name, args) => { Check(name == "GetEquipmentPage", "Unexpected equipment call"); return new EquipmentIndexViewModel { TotalEquipment = 8, AvailableCount = 3 }; });
var reservations = Stub<IReservationService>((name, args) => { Check(name == "GetReservations" && (string)args[0] == ReservationStatuses.Pending, "Wrong reservation scope"); return new ReservationListViewModel { Counts = new() { [ReservationStatuses.Pending] = 2, [ReservationStatuses.Borrowed] = 3, [ReservationStatuses.Overdue] = 1 } }; });
var reports = Stub<IReportService>((name, args) => { Check(name == "GetBorrowingReport" && ((BorrowingReportFilter)args[0]).Status == ReservationDisplayStatuses.Overdue && (int)args[2] == 4, "Overdue must use owning report filter"); return new BorrowingReportViewModel { Summary = new() { TotalRecords = 1 } }; });
var borrowers = Stub<IBorrowerProfileService>((name, args) => { Check(name == "GetPage", "Unexpected borrower mutation"); return new BorrowerPageViewModel { OverallEligible = 7, OverallSuspended = 1 }; });
var maintenance = Stub<IMaintenanceService>((name, args) => { Check(name == "GetPage", "Unexpected maintenance mutation"); return new MaintenancePageViewModel { RecordsAvailable = true, TotalCount = (string)args[1] == MaintenanceStatuses.Scheduled ? 2 : 1 }; });
var service = new AdminDashboardService(accounts, equipment, reservations, reports, borrowers, maintenance, NullLogger<AdminDashboardService>.Instance);
var model = service.GetOverview();
Check(model.PendingRegistrations == 5 && model.RegistrationPreview.Count() == 4, "Registration total/preview mismatch");
Check(model.ReservationCount("Missing") == 0 && model.ReservationCount(ReservationStatuses.Pending) == 2, "Missing stored status should be zero after successful read");
Check(model.ScheduledMaintenance.TotalCount == 2 && model.OngoingMaintenance.TotalCount == 1, "Maintenance states merged");
failAccounts = true;
model = service.GetOverview();
Check(model.Accounts == null && model.PendingRegistrations == null && !model.RegistrationPreview.Any(), "Failure must not look like zero registrations");
Check(model.Equipment.TotalEquipment == 8 && model.Overdue.Summary.TotalRecords == 1, "Account failure must not hide healthy sections");
failAccounts = false; empty = true;
model = service.GetOverview();
Check(model.PendingRegistrations == 0 && model.Accounts.Count == 0, "Genuine zero must remain distinguishable");
Check(new AdminDashboardViewModel().ReservationCount(ReservationStatuses.Pending) == null, "Unavailable reservation count must stay null");
Console.WriteLine("PASS: read-only composition, registration preview, overdue filter, maintenance states, partial failure, and zero/unavailable semantics.");
public class ReadProxy : DispatchProxy
{
    public Func<string, object[], object> Read { get; set; }
    protected override object Invoke(MethodInfo method, object[] args) => Read(method.Name, args);
}
