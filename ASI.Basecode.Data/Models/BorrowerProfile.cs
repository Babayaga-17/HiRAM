using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models;

public partial class BorrowerProfile
{
    public int UserId { get; set; }

    public int DepartmentId { get; set; }

    public string Program { get; set; }

    public byte? YearLevel { get; set; }

    public string EligibilityStatus { get; set; }

    public string SuspensionReason { get; set; }

    public int? SuspendedByUserId { get; set; }

    public DateTime? SuspendedAt { get; set; }

    public DateTime? SuspendedUntil { get; set; }

    public string CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public string UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Department Department { get; set; }

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public virtual User SuspendedByUser { get; set; }

    public virtual User User { get; set; }
}
