using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models;

public partial class MaintenanceRecord
{
    public int MaintenanceId { get; set; }

    public int EquipmentId { get; set; }

    public string MaintenanceType { get; set; }

    public string Description { get; set; }

    public string Status { get; set; }

    public DateTime StartDateTime { get; set; }

    public DateTime? EndDateTime { get; set; }

    public int? ReportedByUserId { get; set; }

    public int? CompletedByUserId { get; set; }

    public string Remarks { get; set; }

    public string CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public string UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User CompletedByUser { get; set; }

    public virtual Equipment Equipment { get; set; }

    public virtual User ReportedByUser { get; set; }
}
