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
    private readonly IUserAccountService _userAccountsService;
    private readonly IEquipmentService _equipmentService;
    public DashboardService(
                            IReservationRepository reservationRepository,
                            IReservationService reservationService,
                            IUserAccountService userAccountsService, 
                            IEquipmentService equipmentService)
    {
        this._reservationRepository = reservationRepository;
        this._reservationService = reservationService;
        this._userAccountsService = userAccountsService;
        this._equipmentService = equipmentService; 
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

        int Count(string status) => counts.TryGetValue(status, out var count) ? count : 0;

        var pendingRequests = await _reservationService.GetPendingReservationPreviewAsync();

        return new CustodianDashboardViewModel
        {
            PendingCount = Count(ReservationStatuses.Pending),
            ApprovedCount = Count(ReservationStatuses.Approved),
            OutstandingCount =
                Count(ReservationStatuses.Borrowed) +
                Count(ReservationStatuses.Overdue),
            PendingRequests = pendingRequests
        };
    }

    public async Task<AdminDashboardViewModel> GetAdminOverviewAsync()
    {
        var counts = await _userAccountsService.CountAccountsByStatusAsync();
        var preview = await _userAccountsService.GetPendingRegistrationPreviewAsync();
        var totalEquipment = await _equipmentService.CountEquipmentAsync();

        return new AdminDashboardViewModel
        {
            TotalUsers = counts.Values.Sum(),
            PendingRegistrationCount =
                counts.TryGetValue(
                    AccountStatuses.PendingApproval,
                    out var pendingCount)
                ? pendingCount
                : 0,
            TotalEquipment = totalEquipment,
            RegistrationPreview = preview
        };
    }

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

    #endregion
}
