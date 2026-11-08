using System;
using System.Linq;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.Extensions.Logging;
namespace ASI.Basecode.Services.Services;

public class DashboardService : IDashboardService
{
    private readonly IUserAccountService accounts;
    private readonly IBorrowerProfileService borrowers;
    private readonly IEquipmentReservationRepository borrowerReservations;
    private readonly IReservationService reservations;
    private readonly IReportService reports;
    private readonly IEquipmentService equipment;
    private readonly IMaintenanceService maintenance;
    private readonly ILogger<DashboardService> logger;
    public DashboardService(IUserAccountService accounts, 
                            IBorrowerProfileService borrowers,
                            IEquipmentReservationRepository borrowerReservations, 
                            IReservationService reservations,
                            IReportService reports, 
                            IEquipmentService equipment, 
                            IMaintenanceService maintenance, 
                            ILogger<DashboardService> logger)
    {
        this.accounts = accounts;
        this.borrowers = borrowers;
        this.borrowerReservations = borrowerReservations; 
        this.reservations = reservations;
        this.reports = reports; 
        this.equipment = equipment; 
        this.maintenance = maintenance; 
        this.logger = logger;
    }

    public BorrowerDashboardViewModel GetBorrowerOverview(int borrowerUserId)
    {
        if (borrowerUserId <= 0) throw new ArgumentOutOfRangeException(nameof(borrowerUserId));
        // Same outstanding/due-date rule and local-clock convention as ReportService's Overdue filter.
        var now = DateTime.Now;
        var model = new BorrowerDashboardViewModel
        {
            Reservations = Read("borrower reservations", () => borrowerReservations.GetBorrowerReservations(borrowerUserId)
                .Where(r => r.BorrowerUserId == borrowerUserId).Select(r => new DashboardReservation
                {
                    ReservationId = r.ReservationId, ReservationNumber = r.ReservationNumber, Status = r.Status,
                    RequestedAt = r.RequestedAt, PickupAt = r.StartDateTime, ReturnBy = r.ExpectedReturnDateTime,
                    EquipmentSummary = string.Join(", ", r.ReservationItems.Where(i => (r.Status != ReservationStatuses.Borrowed && r.Status != ReservationStatuses.Overdue) || !i.IsReturned).Select(i => i.Equipment?.EquipmentName ?? "Equipment")),
                    IsOverdue = (r.Status == ReservationStatuses.Borrowed || r.Status == ReservationStatuses.Overdue) && r.ExpectedReturnDateTime < now
                }).ToList())
        };
        try
        {
            var profile = borrowerReservations.GetBorrowerProfile(borrowerUserId);
            model.ProfileLoaded = true;
            model.EligibilityStatus = profile?.EligibilityStatus;
            model.BorrowerType = profile?.BorrowerType;
        }
        catch (Exception exception) { logger.LogWarning(exception, "Borrower dashboard profile could not be loaded."); }
        return model;
    }
    public CustodianDashboardViewModel GetCustodianOverview() => new()
    {
        Pending = Read("pending requests", () => reservations.GetReservations(ReservationStatuses.Pending)),
        Approved = Read("approved reservations", () => reservations.GetReservations(ReservationStatuses.Approved)),
        Borrowed = Read("borrowed reservations", () => reservations.GetReservations(ReservationStatuses.Borrowed)),
        Overdue = Read("overdue loans", () => reports.GetBorrowingReport(new BorrowingReportFilter { Status = ReservationDisplayStatuses.Overdue }, 1, 4)),
        Equipment = Read("equipment", () => equipment.GetEquipmentPage(new EquipmentListRequest())),
        ScheduledMaintenance = Read("scheduled maintenance", () => maintenance.GetPage(null, MaintenanceStatuses.Scheduled, 1)),
        OngoingMaintenance = Read("ongoing maintenance", () => maintenance.GetPage(null, MaintenanceStatuses.InProgress, 1))
    };

    public AdminDashboardViewModel GetAdminOverview() => new()
    {
        Accounts = Read("accounts", () => accounts.GetUserAccounts()),
        Equipment = Read("equipment", () => equipment.GetEquipmentPage(new EquipmentListRequest())),
        Reservations = Read("reservations", () => reservations.GetReservations(ReservationStatuses.Pending)),
        Overdue = Read("overdue loans", () => reports.GetBorrowingReport(new BorrowingReportFilter { Status = ReservationDisplayStatuses.Overdue }, 1, 4)),
        Borrowers = Read("borrowers", () => borrowers.GetPage(null, null, 1, 1)),
        ScheduledMaintenance = Read("scheduled maintenance", () => maintenance.GetPage(null, MaintenanceStatuses.Scheduled, 1)),
        OngoingMaintenance = Read("ongoing maintenance", () => maintenance.GetPage(null, MaintenanceStatuses.InProgress, 1))
    };

    private T Read<T>(string section, Func<T> read) where T : class
    {
        try { return read(); }
        catch (Exception exception) { logger.LogWarning(exception, "Role dashboard {Section} could not be loaded.", section); return null; }
    }
}
