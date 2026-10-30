using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    public class ReservationDetailsViewModel : PendingReservationListItem
    {
        public string ContactNumber { get; set; }
        public string Program { get; set; }
        public byte? YearLevel { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string ApprovedByName { get; set; }
        public string ApprovalRemarks { get; set; }
        public DateTime? RejectedAt { get; set; }
        public string RejectionReason { get; set; }
        public DateTime? BorrowedAt { get; set; }
        public DateTime? ReturnedAt { get; set; }
        public List<ReservationItemViewModel> Items { get; set; } = new();
    }

}
