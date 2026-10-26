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

    public class PendingReservationItemViewModel
    {
        public string EquipmentName { get; set; }
        public string EquipmentCode { get; set; }
        public string ConditionStatus { get; set; }
        public string StorageLocation { get; set; }
    }

    public class PendingReservationDetailsViewModel : PendingReservationListItem
    {
        public string ContactNumber { get; set; }
        public string Program { get; set; }
        public byte? YearLevel { get; set; }
        public List<PendingReservationItemViewModel> Items { get; set; } = new();
    }
}
