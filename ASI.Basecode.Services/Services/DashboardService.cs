using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.ServiceModels.Dashboard;
using DocumentFormat.OpenXml.VariantTypes;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace ASI.Basecode.Services.Services;

public class DashboardService : IDashboardService
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IReservationService _reservationService;

    private readonly IUserAccountService accounts;
    private readonly IBorrowerProfileService borrowers;
    private readonly IReportService reports;
    private readonly IEquipmentService equipment;
    private readonly IMaintenanceService maintenance;
    private readonly ILogger<DashboardService> logger;
    public DashboardService(
                            IReservationRepository reservationRepository,
                            IReservationService reservationService,
                            IUserAccountService accounts, 
                            IBorrowerProfileService borrowers,
                            IReportService reports, 
                            IEquipmentService equipment, 
                            IMaintenanceService maintenance, 
                            ILogger<DashboardService> logger)
    {
        this._reservationRepository = reservationRepository;
        this._reservationService = reservationService;


        this.accounts = accounts;
        this.borrowers = borrowers;
        this.reports = reports; 
        this.equipment = equipment; 
        this.maintenance = maintenance; 
        this.logger = logger;
    }

    public async Task<BorrowerDashboardViewModel> GetBorrowerOverviewAsync(int borrowerUserId)
    {
        if (borrowerUserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(borrowerUserId));

        var now = DateTime.Now;

        var reservations = await _reservationRepository.GetBorrowerDashboardReservationsAsync(borrowerUserId, now);
        var cards = reservations
            .Select(reservation => MapDashboardReservation(reservation, now))
            .ToList();
        return new BorrowerDashboardViewModel
        {
            Reservations = cards
        };
    }

    public async Task<CustodianDashboardViewModel> GetCustodianOverviewAsync()
    {
        var counts = await _reservationRepository.CountByStatus();

        int? Count(string status)
        {
            if (counts == null)
                return null;

            return counts.TryGetValue(status, out var count) ? count : 0;
        }

        List<PendingReservationListItem> pendingRequests = null;

        try
        {
            pendingRequests = await _reservationService.GetPendingReservationPreviewAsync();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Custodian dashboard pending requests could not be loaded.");
        }

        return new CustodianDashboardViewModel
        {
            PendingCount = Count(ReservationStatuses.Pending),
            ApprovedCount = Count(ReservationStatuses.Approved),
            OutstandingCount = Count(ReservationStatuses.Borrowed) + Count(ReservationStatuses.Overdue),
            PendingRequests = pendingRequests
        };
    }

    public AdminDashboardViewModel GetAdminOverview() => new()
    {
        Accounts = Read("accounts", () => accounts.GetUserAccounts()),
        Equipment = Read("equipment", () => equipment.GetEquipmentPage(new EquipmentListRequest())),
        Overdue = Read("overdue loans", () => reports.GetBorrowingReport(new BorrowingReportFilter { Status = ReservationStatuses.Overdue }, 1, 4)),
        Borrowers = Read("borrowers", () => borrowers.GetPage(null, null, 1, 1)),
        ScheduledMaintenance = Read("scheduled maintenance", () => maintenance.GetPage(null, MaintenanceStatuses.Scheduled, 1)),
        OngoingMaintenance = Read("ongoing maintenance", () => maintenance.GetPage(null, MaintenanceStatuses.InProgress, 1))
    };

    #region Helper

    private static DashboardReservation MapDashboardReservation(Reservation reservation, DateTime now)
    {
        var outstanding =
            reservation.Status == ReservationStatuses.Borrowed ||
            reservation.Status == ReservationStatuses.Overdue;

        var equipmentNames = reservation.ReservationItems
            .Where(item => !outstanding || !item.IsReturned)
            .Select(item => item.Equipment?.EquipmentName ?? "Equipment")
            .ToList();

        return new DashboardReservation
        {
            ReservationId = reservation.ReservationId,
            ReservationNumber = reservation.ReservationNumber,
            Status = reservation.Status,
            RequestedAt = reservation.RequestedAt,
            PickupAt = reservation.StartDateTime,
            ReturnBy = reservation.ExpectedReturnDateTime,
            EquipmentNames = equipmentNames,
            EquipmentSummary = string.Join(", ", equipmentNames),
            IsOverdue =
                outstanding &&
                reservation.ExpectedReturnDateTime < now
        };
    }

    private T Read<T>(string section, Func<T> read) where T : class
    {
        try { return read(); }
        catch (Exception exception) { logger.LogWarning(exception, "Role dashboard {Section} could not be loaded.", section); return null; }
    }

    #endregion
}
