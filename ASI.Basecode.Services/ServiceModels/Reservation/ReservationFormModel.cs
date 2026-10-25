using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels.Reservation;

public class ReservationFormModel : IValidatableObject
{
    public List<int> EquipmentIds { get; set; } = new();
    public List<EquipmentListItem> Equipment { get; set; } = new();

    [Required(ErrorMessage = "Choose a pickup date and time.")]
    public DateTime? Pickup { get; set; }

    [Required(ErrorMessage = "Choose an expected return date and time.")]
    public DateTime? ExpectedReturn { get; set; }

    [Required(ErrorMessage = "Enter the purpose of borrowing.")]
    [StringLength(500, ErrorMessage = "Keep the purpose within 500 characters.")]
    public string Purpose { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));
        if (Pickup.HasValue && Pickup.Value < now)
            yield return new ValidationResult("Pickup must be in the future.", new[] { nameof(Pickup) });
        if (Pickup.HasValue && ExpectedReturn.HasValue && ExpectedReturn.Value <= Pickup.Value)
            yield return new ValidationResult("Return must be after pickup.", new[] { nameof(ExpectedReturn) });
        if (EquipmentIds == null || EquipmentIds.Count == 0)
            yield return new ValidationResult("Select equipment before submitting.");
    }
}
