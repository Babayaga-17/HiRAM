using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    public class ReservationItemViewModel
    {
        public int ReservationItemId { get; set; }
        public string EquipmentName { get; set; }
        public string EquipmentCode { get; set; }
        public string ConditionStatus { get; set; }          // equipment's current condition
        public string StorageLocation { get; set; }
        public string ConditionAtBorrowing { get; set; }     // recorded at release
        public string BorrowingRemarks { get; set; }
    }

}
