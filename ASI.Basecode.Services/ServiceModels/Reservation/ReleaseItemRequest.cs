using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels.Reservation
{
    public class ReleaseItemRequest
    {
        public int ReservationItemId { get; set; }
        public string ConditionAtBorrowing { get; set; }
        public string BorrowingRemarks { get; set; }
    }
}
