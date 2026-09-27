using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models;

public partial class Role
{
    public byte RoleId { get; set; }

    public string RoleName { get; set; }

    public string Description { get; set; }

    public bool IsActive { get; set; }

    public string CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public string UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Account> Accounts { get; set; } = new List<Account>();
}
