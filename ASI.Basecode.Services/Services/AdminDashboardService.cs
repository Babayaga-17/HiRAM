using System;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.Extensions.Logging;
namespace ASI.Basecode.Services.Services;

// Composes existing read services; availability and overdue rules stay with their owning services.
public class AdminDashboardService : IAdminDashboardService
{
    private readonly IUserAccountService accounts;
    private readonly IEquipmentService equipment;
    private readonly IReservationService reservations;
    private readonly IReportService reports;
    private readonly IBorrowerProfileService borrowers;
    private readonly IMaintenanceService maintenance;
    private readonly ILogger<AdminDashboardService> logger;
    public AdminDashboardService(IUserAccountService accounts, IEquipmentService equipment,
        IReservationService reservations, IReportService reports, IBorrowerProfileService borrowers,
        IMaintenanceService maintenance, ILogger<AdminDashboardService> logger)
    {
        this.accounts = accounts; this.equipment = equipment; this.reservations = reservations;
        this.reports = reports; this.borrowers = borrowers; this.maintenance = maintenance; this.logger = logger;
    }
    public AdminDashboardViewModel GetOverview() => new()
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
        catch (Exception exception) { logger.LogWarning(exception, "Admin dashboard {Section} could not be loaded.", section); return null; }
    }
}
