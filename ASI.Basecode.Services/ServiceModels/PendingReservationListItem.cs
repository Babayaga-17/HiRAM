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

    // ----- Approval and Release (new) -----

    public class ReservationListViewModel
    {
        public string Status { get; set; } = "Pending";                  // active tab
        public List<PendingReservationListItem> Items { get; set; } = [];
        public Dictionary<string, int> Counts { get; set; } = new();     // reservations per status
        public int TotalCount { get; set; }
    }

    public class ReservationItemViewModel
    {
        public int ReservationItemId { get; set; }
        public string EquipmentName { get; set; }
        public string EquipmentCode { get; set; }
        public string ConditionStatus { get; set; }          // equipment's current condition
        public string StorageLocation { get; set; }
        public string ConditionAtBorrowing { get; set; }     // recorded at release
        public string BorrowingRemarks { get; set; }
    }

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

    public class ReleaseItemRequest
    {
        public int ReservationItemId { get; set; }
        public string ConditionAtBorrowing { get; set; }
        public string BorrowingRemarks { get; set; }
    }

    public class ReleaseReservationRequest
    {
        public int ReservationId { get; set; }
        public DateTime? ReleasedAt { get; set; }          // empty = now
        public List<ReleaseItemRequest> Items { get; set; } = new();
    }
}
