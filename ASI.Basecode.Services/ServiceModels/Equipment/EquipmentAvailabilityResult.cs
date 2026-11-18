using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    public class EquipmentBusyPeriod
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class EquipmentFreeSlot
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    public class EquipmentAvailabilityResult
    {
        public bool IsAvailable { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<EquipmentBusyPeriod> Periods { get; set; } = new List<EquipmentBusyPeriod>();
        public List<EquipmentFreeSlot> Slots { get; set; } = new List<EquipmentFreeSlot>();
    }
}