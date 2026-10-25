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
    private readonly IEquipmentReservationRepository _reservationRepository;
    private readonly IEquipmentService _equipmentService;

    public EquipmentReservationService(
        IEquipmentReservationRepository reservationRepository,
        IEquipmentService equipmentService)
    {
        _reservationRepository = reservationRepository;
        _equipmentService = equipmentService;
    }

    public (string ReservationNumber, string Error) Submit(ReservationFormModel form, int borrowerUserId)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(form, new ValidationContext(form), errors, true))
            return (null, errors.First().ErrorMessage);

        var ids = form.EquipmentIds.Distinct().OrderBy(id => id).ToList();
        if (ids.Any(id => id <= 0))
            return (null, "Review your equipment selection.");

        var number = "RES-" + Guid.NewGuid().ToString("N").Substring(0, 26);
        var error = ExecuteChange(() =>
        {
            var profile = _reservationRepository.GetBorrowerProfile(borrowerUserId);
            if (profile?.EligibilityStatus != "Eligible"
                || profile.User?.Account?.AccountStatus != "Active"
                || profile.User.Account.Role?.RoleName != "Borrower")
                return "Your account needs an eligible borrower profile before you can reserve equipment. Contact the custodian.";

            var selectionError = ValidateEquipment(ids, form.Pickup.Value, form.ExpectedReturn.Value);
            if (selectionError != null)
                return selectionError;

            var now = PhilippineTimeNow();
            var actor = borrowerUserId.ToString();
            _reservationRepository.AddReservation(new Reservation
            {
                ReservationNumber = number,
                BorrowerUserId = borrowerUserId,
                Purpose = form.Purpose.Trim(),
                StartDateTime = form.Pickup.Value,
                ExpectedReturnDateTime = form.ExpectedReturn.Value,
                Status = "Pending",
                RequestedAt = now,
                CreatedAt = now,
                CreatedBy = actor,
                ReservationItems = ids.Select(id => new ReservationItem
                {
                    EquipmentId = id, CreatedAt = now, CreatedBy = actor
                }).ToList()
            });
            return null;
        }, "Equipment availability changed while submitting. Please try again.");

        return error == null ? (number, null) : (null, error);
    }

    public List<ReservationViewModel> GetPendingReservations(int borrowerUserId)
    {
        return _reservationRepository.GetPendingReservations(borrowerUserId)
            .Select(ToViewModel).ToList();
    }

    public ReservationViewModel GetReservationDetails(int reservationId, int borrowerUserId)
    {
        var reservation = _reservationRepository.GetReservationDetails(reservationId, borrowerUserId);
        return reservation == null ? null : ToViewModel(reservation);
    }

    public string Edit(int reservationId, ReservationEditFormModel form, int borrowerUserId)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(form, new ValidationContext(form), errors, true))
            return errors.First().ErrorMessage;

        var version = ReadRowVersion(form.RowVersion);
        if (version == null)
            return "Reload the reservation before editing.";

        var ids = form.EquipmentIds.Distinct().OrderBy(id => id).ToList();
        if (ids.Any(id => id <= 0))
            return "Review your equipment selection.";

        return ExecuteChange(() =>
        {
            var reservation = _reservationRepository.GetReservationForUpdate(reservationId, borrowerUserId);
            if (reservation == null)
                return "Reservation not found.";
            if (reservation.Status != "Pending")
                return "Only pending reservations can be edited.";
            if (!reservation.RowVersion.SequenceEqual(version))
                return "This reservation has changed. Reload it before editing.";

            var selectionError = ValidateEquipment(ids, form.Pickup.Value, form.ExpectedReturn.Value, reservationId);
            if (selectionError != null)
                return selectionError;

            var now = PhilippineTimeNow();
            var actor = borrowerUserId.ToString();
            var removedItems = reservation.ReservationItems
                .Where(item => !ids.Contains(item.EquipmentId)).ToList();
            _reservationRepository.RemoveReservationItems(removedItems);

            var existingIds = reservation.ReservationItems.Select(item => item.EquipmentId).ToHashSet();
            foreach (var id in ids.Where(id => !existingIds.Contains(id)))
            {
                reservation.ReservationItems.Add(new ReservationItem
                {
                    EquipmentId = id, CreatedAt = now, CreatedBy = actor
                });
            }

            reservation.StartDateTime = form.Pickup.Value;
            reservation.ExpectedReturnDateTime = form.ExpectedReturn.Value;
            reservation.Purpose = form.Purpose.Trim();
            reservation.UpdatedBy = actor;
            reservation.UpdatedAt = now;
            return null;
        }, "This reservation changed while you were editing. Reload it and try again.");
    }

    public string Cancel(int reservationId, ReservationCancelFormModel form, int borrowerUserId)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(form, new ValidationContext(form), errors, true))
            return errors.First().ErrorMessage;

        var version = ReadRowVersion(form.RowVersion);
        if (version == null)
            return "Reload the reservation before cancelling.";

        return ExecuteChange(() =>
        {
            var reservation = _reservationRepository.GetReservationForUpdate(reservationId, borrowerUserId);
            if (reservation == null)
                return "Reservation not found.";
            if (reservation.Status != "Pending")
                return "Only pending reservations can be cancelled.";
            if (!reservation.RowVersion.SequenceEqual(version))
                return "This reservation has changed. Reload it before cancelling.";

            var now = PhilippineTimeNow();
            reservation.Status = "Cancelled";
            reservation.CancelledAt = now;
            reservation.CancellationReason = string.IsNullOrWhiteSpace(form.Reason) ? null : form.Reason.Trim();
            reservation.UpdatedBy = borrowerUserId.ToString();
            reservation.UpdatedAt = now;
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

    private string ExecuteChange(Func<string> operation, string concurrencyMessage)
    {
        try
        {
            return _reservationRepository.ExecuteInTransaction(operation);
        }
        catch (ReservationConcurrencyException)
        {
            return concurrencyMessage;
        }
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
