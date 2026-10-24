using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models;

public partial class PasswordResetToken
{
    public int TokenId { get; set; }

    public int AccountId { get; set; }

    public string TokenHash { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public string CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public string UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Account Account { get; set; }
}
