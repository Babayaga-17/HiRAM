using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    public enum EquipmentAvailability
    {
        Available,
        Reserved,
        Borrowed,
        UnderMaintenance,
        Unavailable
    }

    public class EquipmentListItem
    {
        public int EquipmentId { get; set; }
        public string EquipmentCode { get; set; } = string.Empty;
        public string AssetTag { get; set; }
        public string EquipmentName { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string Brand { get; set; }
        public string Model { get; set; }
        public string SerialNumber { get; set; }
        public string Description { get; set; }
        public string ConditionStatus { get; set; } = string.Empty;
        public string StorageLocation { get; set; }
        public DateOnly? AcquisitionDate { get; set; }
        public decimal? AcquisitionCost { get; set; }
        public bool IsActive { get; set; }
        public EquipmentAvailability Status { get; set; }
        public DateTime? NextReservationStart { get; set; }

        public string StatusLabel => Status switch
        {
            EquipmentAvailability.Available => "Available",
            EquipmentAvailability.Reserved => "Reserved",
            EquipmentAvailability.Borrowed => "Borrowed",
            EquipmentAvailability.UnderMaintenance => "Under Maintenance",
            _ => "Unavailable"
        };

        public string StatusClass => Status.ToString().ToLowerInvariant();
    }
}