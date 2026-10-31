using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    public class ReleaseReservationRequest
    {
        public int ReservationId { get; set; }
        public DateTime? ReleasedAt { get; set; }          // empty = now
        public List<ReleaseItemRequest> Items { get; set; } = new();
    }

}
