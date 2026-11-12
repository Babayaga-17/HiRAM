using ASI.Basecode.Resources.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels.Dashboard
{
    public class CustodianDashboardViewModel
    {
        public int? PendingCount { get; set; }
        public int? ApprovedCount { get; set; }
        public int? OutstandingCount { get; set; }

        public List<PendingReservationListItem> PendingRequests { get; set; }
    }
}
