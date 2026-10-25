using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.ServiceModels.Reservation;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Services.Services;

public class EquipmentReservationService : IEquipmentReservationService
{
    private readonly HiramDbContext _db;
    private readonly IEquipmentService _equipmentService;

    public EquipmentReservationService(HiramDbContext db, IEquipmentService equipmentService)
    {
        _db = db;
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

        // Keep eligibility checks, overlap reads and insertion in one transaction.
        // Serializable isolation prevents concurrent requests from both reserving the same slot.
        using var transaction = _db.Database.BeginTransaction(IsolationLevel.Serializable);

        var eligible = _db.BorrowerProfiles.Any(
            profile => profile.UserId == borrowerUserId
            && profile.EligibilityStatus == "Eligible"
            && profile.User.Account.AccountStatus == "Active"
            && profile.User.Account.Role.RoleName == "Borrower");
        if (!eligible)
            return (null, "Your account needs an eligible borrower profile before you can reserve equipment. Contact the custodian.");

        foreach (var id in ids)
        {
            var item = _db.Equipments.Where(equipment => equipment.EquipmentId == id)
                .Select(equipment => new { equipment.EquipmentName, equipment.CategoryId,
                    CategoryActive = equipment.Category != null && equipment.Category.IsActive })
                .SingleOrDefault();
            if (item == null || (item.CategoryId.HasValue && !item.CategoryActive))
                return (null, "Some equipment is no longer available. Review your selection.");

            var availability = _equipmentService.GetAvailability(id, form.Pickup.Value, form.ExpectedReturn.Value);
            if (!availability.IsAvailable)
                return (null, item.EquipmentName + ": " + availability.Reason);
        }

        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));
        var actor = borrowerUserId.ToString();
        var reservation = new Reservation
        {
            ReservationNumber = "RES-" + Guid.NewGuid().ToString("N").Substring(0, 26),
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
        };
        _db.Reservations.Add(reservation);
        _db.SaveChanges();
        transaction.Commit();
        return (reservation.ReservationNumber, null);
    }

    public List<ReservationViewModel> GetPendingReservations(int borrowerUserId)
    {
        return ReservationsForBorrower(borrowerUserId)
            .Where(reservation => reservation.Status == "Pending")
            .OrderByDescending(reservation => reservation.RequestedAt)
            .ThenByDescending(reservation => reservation.ReservationId)
            .ToList();
    }

    public ReservationViewModel GetReservationDetails(
        int reservationId,
        int borrowerUserId)
    {
        return ReservationsForBorrower(borrowerUserId)
            .SingleOrDefault(reservation =>
                reservation.ReservationId == reservationId);
    }

    private IQueryable<ReservationViewModel> ReservationsForBorrower(int borrowerUserId)
    {
        return _db.Reservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.BorrowerUserId == borrowerUserId)
            .Select(reservation => new ReservationViewModel
            {
                ReservationId = reservation.ReservationId,
                ReservationNumber = reservation.ReservationNumber,
                Status = reservation.Status,
                Purpose = reservation.Purpose,
                RequestedAt = reservation.RequestedAt,
                StartDateTime = reservation.StartDateTime,
                ExpectedReturnDateTime =
                    reservation.ExpectedReturnDateTime,

                ApprovedAt = reservation.ApprovedAt,
                RejectedAt = reservation.RejectedAt,
                BorrowedAt = reservation.BorrowedAt,
                ReturnedAt = reservation.ReturnedAt,
                CancelledAt = reservation.CancelledAt,

                RejectionReason = reservation.RejectionReason,
                CancellationReason = reservation.CancellationReason,

                Equipment = reservation.ReservationItems
                    .OrderBy(item => item.Equipment.EquipmentName)
                    .Select(item => new EquipmentListItem
                    {
                        EquipmentId = item.EquipmentId,
                        EquipmentName = item.Equipment.EquipmentName,
                        EquipmentCode = item.Equipment.EquipmentCode
                    })
                    .ToList(),

                RowVersion = reservation.RowVersion,
            });
    }

    public string Edit(int reservationId, ReservationEditFormModel form, int borrowerUserId)
    {
        var errors = new List<ValidationResult>();

        if (!Validator.TryValidateObject(
            form, new ValidationContext(form), errors, true))
        {
            return errors.First().ErrorMessage;
        }

        var version = ReadRowVersion(form.RowVersion);

        if (version == null)
            return "Reload the reservation before editing.";

        var ids = form.EquipmentIds
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        if (ids.Any(id => id <= 0))
            return "Review your equipment selection.";

        using var transaction =
            _db.Database.BeginTransaction(IsolationLevel.Serializable);

        var reservation = _db.Reservations
            .Include(item => item.ReservationItems)
            .SingleOrDefault(item =>
                item.ReservationId == reservationId
                && item.BorrowerUserId == borrowerUserId);

        if (reservation == null)
            return "Reservation not found.";

        if (reservation.Status != "Pending")
            return "Only pending reservations can be edited.";

        if (!reservation.RowVersion.SequenceEqual(version))
            return "This reservation has changed. Reload it before editing.";

        foreach (var id in ids)
        {
            var equipment = _db.Equipments
                .Where(item => item.EquipmentId == id)
                .Select(item => new
                {
                    item.EquipmentName,
                    item.CategoryId,
                    CategoryActive =
                        item.Category != null && item.Category.IsActive
                })
                .SingleOrDefault();

            if (equipment == null
                || (equipment.CategoryId.HasValue
                    && !equipment.CategoryActive))
            {
                return "Some equipment is no longer available. Review your selection.";
            }

            var availability = _equipmentService.GetAvailability(
                id,
                form.Pickup.Value,
                form.ExpectedReturn.Value,
                reservationId);

            if (!availability.IsAvailable)
                return equipment.EquipmentName + ": " + availability.Reason;
        }

        var now = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));

        var actor = borrowerUserId.ToString();

        // Remove only the items the borrower deselected.
        var removedItems = reservation.ReservationItems
            .Where(item => !ids.Contains(item.EquipmentId))
            .ToList();

        _db.ReservationItems.RemoveRange(removedItems);

        // Keep existing items and add only newly selected equipment.
        var existingIds = reservation.ReservationItems
            .Select(item => item.EquipmentId)
            .ToHashSet();

        foreach (var id in ids.Where(id => !existingIds.Contains(id)))
        {
            reservation.ReservationItems.Add(new ReservationItem
            {
                EquipmentId = id,
                CreatedBy = actor,
                CreatedAt = now
            });
        }

        reservation.StartDateTime = form.Pickup.Value;
        reservation.ExpectedReturnDateTime = form.ExpectedReturn.Value;
        reservation.Purpose = form.Purpose.Trim();
        reservation.UpdatedBy = actor;
        reservation.UpdatedAt = now;

        try
        {
            _db.SaveChanges();
            transaction.Commit();
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return "This reservation changed while you were editing. Reload it and try again.";
        }
    }

    public string Cancel(int reservationId, ReservationCancelFormModel form, int borrowerUserId)
    {
        var errors = new List<ValidationResult>();

        if (!Validator.TryValidateObject(
            form, new ValidationContext(form), errors, true))
        {
            return errors.First().ErrorMessage;
        }

        var version = ReadRowVersion(form.RowVersion);

        if (version == null)
            return "Reload the reservation before cancelling.";

        var reservation = _db.Reservations
            .SingleOrDefault(item =>
                item.ReservationId == reservationId
                && item.BorrowerUserId == borrowerUserId);

        if (reservation == null)
            return "Reservation not found.";

        if (reservation.Status != "Pending")
            return "Only pending reservations can be cancelled.";

        if (!reservation.RowVersion.SequenceEqual(version))
            return "This reservation has changed. Reload it before cancelling.";

        var now = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));

        reservation.Status = "Cancelled";
        reservation.CancelledAt = now;
        reservation.CancellationReason =
            string.IsNullOrWhiteSpace(form.Reason)
                ? null
                : form.Reason.Trim();

        reservation.UpdatedBy = borrowerUserId.ToString();
        reservation.UpdatedAt = now;

        try
        {
            _db.SaveChanges();
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return "This reservation changed while you were cancelling. Reload it and try again.";
        }
    }

    private static byte[] ReadRowVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

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
