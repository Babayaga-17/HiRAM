using ASI.Basecode.Resources.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels.Dashboard
{
    public class DashboardReservation
    {
        public int ReservationId { get; set; }
        public string ReservationNumber { get; set; }
        public string EquipmentSummary { get; set; }
        public List<string> EquipmentNames { get; set; } = new();
        public string Status { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime PickupAt { get; set; }
        public DateTime ReturnBy { get; set; }
        public bool IsOverdue { get; set; }
        public bool IsOutstanding => Status == ReservationStatuses.Borrowed || Status == ReservationStatuses.Overdue;
    }
}
