using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    public class PendingReservationItemViewModel
    {
        public string EquipmentName { get; set; }
        public string EquipmentCode { get; set; }
        public string ConditionStatus { get; set; }
        public string StorageLocation { get; set; }
    }
}
