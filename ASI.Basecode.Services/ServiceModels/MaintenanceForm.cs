using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
namespace ASI.Basecode.Services.ServiceModels;
public class MaintenanceForm : IValidatableObject
{
    // Limits that stop unrealistic dates (e.g. year 2050) from blocking equipment for years.
    public const int MaxDaysAheadToStart = 365;
    public const int MaxMaintenanceDays = 90;

    [Range(1, int.MaxValue, ErrorMessage = "Select equipment.")] public int EquipmentId { get; set; }
    [Required, StringLength(50)] public string MaintenanceType { get; set; }
    [Required, StringLength(500)] public string Description { get; set; }
    public DateTime StartDateTime { get; set; } = DateTime.Now;
    public DateTime? ExpectedCompletionDateTime { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (StartDateTime == default) yield return new ValidationResult("Enter a start date and time.", new[] { nameof(StartDateTime) });
        else if (StartDateTime > DateTime.Now.AddDays(MaxDaysAheadToStart))
            yield return new ValidationResult($"Start cannot be more than {MaxDaysAheadToStart} days from now.", new[] { nameof(StartDateTime) });
        if (ExpectedCompletionDateTime.HasValue && ExpectedCompletionDateTime <= StartDateTime)
            yield return new ValidationResult("Expected completion must be after the start.", new[] { nameof(ExpectedCompletionDateTime) });
        else if (ExpectedCompletionDateTime.HasValue && ExpectedCompletionDateTime > StartDateTime.AddDays(MaxMaintenanceDays))
            yield return new ValidationResult($"Expected completion cannot be more than {MaxMaintenanceDays} days after the start.", new[] { nameof(ExpectedCompletionDateTime) });
    }
}
