using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
namespace ASI.Basecode.Services.ServiceModels;
public class MaintenanceForm : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Select equipment.")] public int EquipmentId { get; set; }
    [Required, StringLength(50)] public string MaintenanceType { get; set; }
    [Required, StringLength(500)] public string Description { get; set; }
    public DateTime StartDateTime { get; set; } = DateTime.Now;
    public DateTime? ExpectedCompletionDateTime { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (StartDateTime == default) yield return new ValidationResult("Enter a start date and time.", new[] { nameof(StartDateTime) });
        if (ExpectedCompletionDateTime.HasValue && ExpectedCompletionDateTime <= StartDateTime)
            yield return new ValidationResult("Expected completion must be after the start.", new[] { nameof(ExpectedCompletionDateTime) });
    }
}
