using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    public class HistoryRowViewModel
    {
        public string ReservationNumber { get; set; }
        public string Purpose { get; set; }
        public string Status { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime ExpectedReturnDateTime { get; set; }
        public DateTime? BorrowedAt { get; set; }
        public DateTime? ReturnedAt { get; set; }
        public List<HistoryItemViewModel> Items { get; set; } = new();
    }


}
