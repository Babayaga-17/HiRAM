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
        public ReservationListViewModel Pending { get; set; }
        public ReservationListViewModel Approved { get; set; }
        public ReservationListViewModel Borrowed { get; set; }
        public BorrowingReportViewModel Overdue { get; set; }
        public EquipmentIndexViewModel Equipment { get; set; }
        public MaintenancePageViewModel ScheduledMaintenance { get; set; }
        public MaintenancePageViewModel OngoingMaintenance { get; set; }
        public int? Count(string status)
        {
            var source = status == ReservationStatuses.Approved ? Approved
                : status == ReservationStatuses.Borrowed || status == ReservationStatuses.Overdue ? Borrowed : Pending;
            return source == null ? null : source.Counts.TryGetValue(status, out var count) ? count : 0;
        }
        public int? OutstandingCount => Count(ReservationStatuses.Borrowed) + Count(ReservationStatuses.Overdue);
    }
}
