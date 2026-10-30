using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    public class ReservationListViewModel
    {
        public string Status { get; set; } = "Pending";                  // active tab
        public List<PendingReservationListItem> Items { get; set; } = [];
        public Dictionary<string, int> Counts { get; set; } = new();     // reservations per status
        public int TotalCount { get; set; }
    }


}
