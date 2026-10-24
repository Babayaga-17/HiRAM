using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models;

public partial class Account
{
    public int AccountId { get; set; }

    public int UserId { get; set; }

    public byte RoleId { get; set; }

    public string Email { get; set; }

    public string PasswordHash { get; set; }

    public string AccountStatus { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTime? LockoutEndAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public string CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public string UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();

    public virtual Role Role { get; set; }

    public virtual User User { get; set; }
}
