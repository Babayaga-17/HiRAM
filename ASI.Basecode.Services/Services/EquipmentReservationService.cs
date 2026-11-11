using ASI.Basecode.Resources.Constants;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using ASI.Basecode.Data.Exceptions;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.ServiceModels.Reservation;

namespace ASI.Basecode.Services.Services;

public class EquipmentReservationService : IEquipmentReservationService
{
    private readonly IEquipmentReservationRepository _equipmentReservationRepository;
    private readonly IReservationRepository _reservationRepository;
    private readonly IReservationService _reservationService;
    private readonly IEquipmentService _equipmentService;
    private readonly INotificationService _notifications;

    public EquipmentReservationService(IEquipmentReservationRepository equipmentReservationRepository,
                                        IReservationRepository reservationRepository,
                                        IReservationService reservationService,
                                        IEquipmentService equipmentService, 
                                        INotificationService notifications)
    {
        _equipmentReservationRepository = equipmentReservationRepository;
        _reservationRepository = reservationRepository;
        _equipmentService = equipmentService;
        _notifications = notifications;
        _reservationService = reservationService;
    }

    public List<ReservationViewModel> GetPendingReservations(int borrowerUserId)
    {
        return _equipmentReservationRepository.GetPendingReservations(borrowerUserId)
            .Select(ToViewModel).ToList();
    }
    public string Cancel(int reservationId, ReservationCancelFormModel form, int borrowerUserId)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(form, new ValidationContext(form), errors, true))
            return errors.First().ErrorMessage;

        var version = ReadRowVersion(form.RowVersion);
        if (version == null)
            return "Reload the reservation before cancelling.";

        return _reservationService.ExecuteChange(() =>
        {
            var reservation = _equipmentReservationRepository.GetReservationForUpdate(reservationId, borrowerUserId);
            if (reservation == null)
                return "Reservation not found.";
            if (reservation.Status != ReservationStatuses.Pending)
                return "Only pending reservations can be cancelled.";
            if (!reservation.RowVersion.SequenceEqual(version))
                return "This reservation has changed. Reload it before cancelling.";

            var now = PhilippineTimeNow();
            reservation.Status = ReservationStatuses.Cancelled;
            reservation.CancelledAt = now;
            reservation.CancellationReason = string.IsNullOrWhiteSpace(form.Reason) ? null : form.Reason.Trim();
            reservation.UpdatedBy = borrowerUserId.ToString();
            reservation.UpdatedAt = now;
            _notifications.ReservationChanged(reservation, NotificationEvents.ReservationCancelled);
            return null;
        }, "This reservation changed while you were cancelling. Reload it and try again.");
    }

    // Business rules stay here; the repository owns queries, persistence and transactions.
    private string ValidateEquipment(List<int> ids, DateTime pickup, DateTime expectedReturn, int? excludeReservationId = null)
    {
        foreach (var id in ids)
        {
            var equipment = _reservationRepository.GetEquipment(id);
            if (equipment == null || (equipment.CategoryId.HasValue && equipment.Category?.IsActive != true))
                return "Some equipment is no longer available. Review your selection.";

            var availability = _equipmentService.GetAvailability(id, pickup, expectedReturn, excludeReservationId);
            if (!availability.IsAvailable)
                return equipment.EquipmentName + ": " + availability.Reason;
        }
        return null;
    }

    private static ReservationViewModel ToViewModel(Reservation reservation)
    {
        return new ReservationViewModel
        {
            ReservationId = reservation.ReservationId,
            ReservationNumber = reservation.ReservationNumber,
            Status = reservation.Status,
            Purpose = reservation.Purpose,
            RequestedAt = reservation.RequestedAt,
            StartDateTime = reservation.StartDateTime,
            ExpectedReturnDateTime = reservation.ExpectedReturnDateTime,
            ApprovedAt = reservation.ApprovedAt,
            RejectedAt = reservation.RejectedAt,
            BorrowedAt = reservation.BorrowedAt,
            ReturnedAt = reservation.ReturnedAt,
            CancelledAt = reservation.CancelledAt,
            RejectionReason = reservation.RejectionReason,
            CancellationReason = reservation.CancellationReason,
            RowVersion = reservation.RowVersion,
            Equipment = reservation.ReservationItems.OrderBy(item => item.Equipment.EquipmentName)
                .Select(item => new EquipmentListItem
                {
                    EquipmentId = item.EquipmentId,
                    EquipmentName = item.Equipment.EquipmentName,
                    EquipmentCode = item.Equipment.EquipmentCode
                }).ToList()
        };
    }

    private static DateTime PhilippineTimeNow() => TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));

    private static byte[] ReadRowVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            var version = Convert.FromBase64String(value);
            return version.Length == 8 ? version : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
