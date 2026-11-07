using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ASI.Basecode.Data.Models;
namespace ASI.Basecode.Services.ServiceModels;

public class BorrowerRegistrationViewModel : IValidatableObject
{
    [Required, RegularExpression("^(Student|Faculty)$")] public string BorrowerType { get; set; } = "Student";
    [Required, StringLength(30, MinimumLength = 3), RegularExpression(@"^[A-Za-z0-9\-]+$")] public string UserCode { get; set; }
    [Required, StringLength(100), RegularExpression(UserAccountFormViewModel.NamePattern)] public string FirstName { get; set; }
    [StringLength(100), RegularExpression(UserAccountFormViewModel.NamePattern)] public string MiddleName { get; set; }
    [Required, StringLength(100), RegularExpression(UserAccountFormViewModel.NamePattern)] public string LastName { get; set; }
    [StringLength(20)] public string Suffix { get; set; }
    [Required, EmailAddress, StringLength(255)] public string Email { get; set; }
    [StringLength(30), Phone] public string ContactNumber { get; set; }
    [Required, Range(1, int.MaxValue)] public int? DepartmentId { get; set; }
    [StringLength(150)] public string Program { get; set; }
    public byte? YearLevel { get; set; }
    [Required, RegularExpression(UserAccountFormViewModel.PasswordPattern, ErrorMessage = UserAccountFormViewModel.PasswordRuleText)] public string Password { get; set; }
    [Required, Compare(nameof(Password), ErrorMessage = "Passwords must match.")] public string ConfirmPassword { get; set; }
    public List<Department> Departments { get; set; } = new();
    public string DepartmentError { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (BorrowerType == "Student")
        {
            if (string.IsNullOrWhiteSpace(Program)) yield return new ValidationResult("Program is required for students.", new[] { nameof(Program) });
            if (!YearLevel.HasValue || YearLevel < 1 || YearLevel > 6) yield return new ValidationResult("Select a year level.", new[] { nameof(YearLevel) });
        }
    }
}
