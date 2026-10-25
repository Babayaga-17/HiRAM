using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
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
}
