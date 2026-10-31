// ASI.Basecode.Services/ServiceModels/PendingReservationListItem.cs  (your original classes + the new ones at the bottom)
using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels
{
    public class PendingReservationListItem
    {
        public int ReservationId { get; set; }
        public string ReservationNumber { get; set; }
        public string BorrowerName { get; set; }
        public string BorrowerUserCode { get; set; }
        public string DepartmentName { get; set; }
        public string EligibilityStatus { get; set; }
        public string Purpose { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime ExpectedReturnDateTime { get; set; }
        public int ItemCount { get; set; }
        public string EquipmentSummary { get; set; }
        public string Status { get; set; }
    }
}