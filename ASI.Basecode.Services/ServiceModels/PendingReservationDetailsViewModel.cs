using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    public class PendingReservationDetailsViewModel : PendingReservationListItem
    {
        public string ContactNumber { get; set; }
        public string Program { get; set; }
        public byte? YearLevel { get; set; }
        public List<PendingReservationItemViewModel> Items { get; set; } = new();
    }
}
