using ASI.Basecode.Resources.Constants;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// Create / edit user account form. Password fields are used only when creating.
/// Lengths follow the dbo.Users and dbo.Accounts column sizes.
/// </summary>
public class UserAccountFormViewModel : IValidatableObject
{
    public const string NamePattern = @"^[A-Za-zÀ-ÖØ-öø-ÿÑñ .'\-]+$";
    public const string PasswordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,64}$";
    public const string PasswordRuleText = "Password must be 8 to 64 characters and include an uppercase letter, a lowercase letter, and a number.";

    /// <summary>Values allowed by CK_BorrowerProfiles_borrower_type.</summary>
    public static readonly string[] BorrowerTypeOptions = { "Student", "Faculty" };

    /// <summary>Values the form lets an administrator choose. Suspended is set by the suspension flow only.</summary>
    public static readonly string[] EligibilityOptions = { "Eligible", "Ineligible" };

    public int UserId { get; set; }

    [Required(ErrorMessage = "User ID is required.")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "User ID must be 3 to 30 characters.")]
    [RegularExpression(@"^[A-Za-z0-9\-]+$", ErrorMessage = "User ID may only contain letters, numbers, and hyphens.")]
    public string UserCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "First name is required.")]
    [StringLength(100, ErrorMessage = "First name must not exceed 100 characters.")]
    [RegularExpression(NamePattern, ErrorMessage = "First name contains invalid characters.")]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Middle name must not exceed 100 characters.")]
    [RegularExpression(NamePattern, ErrorMessage = "Middle name contains invalid characters.")]
    public string MiddleName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(100, ErrorMessage = "Last name must not exceed 100 characters.")]
    [RegularExpression(NamePattern, ErrorMessage = "Last name contains invalid characters.")]
    public string LastName { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "Suffix must not exceed 20 characters.")]
    [RegularExpression(NamePattern, ErrorMessage = "Suffix contains invalid characters.")]
    public string Suffix { get; set; } = string.Empty;

    [StringLength(30, ErrorMessage = "Contact number must not exceed 30 characters.")]
    [RegularExpression(@"^[0-9+()\- ]{7,30}$", ErrorMessage = "Enter a valid contact number.")]
    public string ContactNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [StringLength(255, ErrorMessage = "Email must not exceed 255 characters.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role is required.")]
    public byte? RoleId { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    public string AccountStatus { get; set; } = AccountStatuses.Active;

    // Borrower details. Required on the server only when the role is Borrower (see UserAccountService).
    public int? DepartmentId { get; set; }

    [StringLength(150, ErrorMessage = "Program must not exceed 150 characters.")]
    public string Program { get; set; } = string.Empty;

    public byte? YearLevel { get; set; }

    public string EligibilityStatus { get; set; } = "Eligible";

    public string BorrowerType { get; set; } = string.Empty;

    /// <summary>True when eligibility cannot be changed here (pending registration or suspended borrower).</summary>
    public bool EligibilityLocked { get; set; }

    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Password and confirmation password must match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>Concurrency token from UserAccountListItem.Version.</summary>
    public string Version { get; set; } = "0";

    public bool IsEdit => UserId > 0;

    public UserAccountFormViewModel() { }

    public UserAccountFormViewModel(UserAccountListItem account)
    {
        UserId = account.UserId;
        UserCode = account.UserCode;
        FirstName = account.FirstName;
        MiddleName = account.MiddleName;
        LastName = account.LastName;
        Suffix = account.Suffix;
        ContactNumber = account.ContactNumber;
        Email = account.Email;
        RoleId = account.RoleId;
        AccountStatus = account.AccountStatus;
        Version = account.Version;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!AccountStatuses.All.Contains(AccountStatus) && !(IsEdit && AccountStatus == AccountStatuses.PendingApproval))
        {
            yield return new ValidationResult("Select a valid status.", new[] { nameof(AccountStatus) });
        }

        if (!IsEdit && !string.IsNullOrEmpty(Password) && !Regex.IsMatch(Password, PasswordPattern))
        {
            yield return new ValidationResult(PasswordRuleText, new[] { nameof(Password) });
        }
    }
}

/// <summary>Active department shown in the Borrower department dropdown.</summary>
public class DepartmentOption
{
    public int DepartmentId { get; set; }

    public string DepartmentName { get; set; } = string.Empty;
}