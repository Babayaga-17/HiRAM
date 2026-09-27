using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models;

public partial class Equipment
{
    public int EquipmentId { get; set; }

    public int CategoryId { get; set; }

    public string EquipmentCode { get; set; }

    public string AssetTag { get; set; }

    public string EquipmentName { get; set; }

    public string Brand { get; set; }

    public string Model { get; set; }

    public string SerialNumber { get; set; }

    public string Description { get; set; }

    public string ConditionStatus { get; set; }

    public string AvailabilityStatus { get; set; }

    public string StorageLocation { get; set; }

    public DateOnly? AcquisitionDate { get; set; }

    public decimal? AcquisitionCost { get; set; }

    public bool IsActive { get; set; }

    public string CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public string UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual EquipmentCategory Category { get; set; }

    public virtual ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();

    public virtual ICollection<ReservationItem> ReservationItems { get; set; } = new List<ReservationItem>();
}
