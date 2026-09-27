using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models;

public partial class AuditLog
{
    public long AuditLogId { get; set; }

    public int? UserId { get; set; }

    public string Action { get; set; }

    public string EntityName { get; set; }

    public string EntityId { get; set; }

    public string Details { get; set; }

    public string IpAddress { get; set; }

    public string CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public string UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User User { get; set; }
}
