using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models;

public partial class Reservation
{
    public int ReservationId { get; set; }

    public string ReservationNumber { get; set; }

    public int BorrowerUserId { get; set; }

    public string Purpose { get; set; }

    public DateTime StartDateTime { get; set; }

    public DateTime ExpectedReturnDateTime { get; set; }

    public string Status { get; set; }

    public DateTime RequestedAt { get; set; }

    public int? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public int? RejectedByUserId { get; set; }

    public DateTime? RejectedAt { get; set; }

    public string RejectionReason { get; set; }

    public DateTime? BorrowedAt { get; set; }

    public DateTime? ReturnedAt { get; set; }

    public int? ReceivedByUserId { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string CancellationReason { get; set; }

    public string CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public string UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; }

    public virtual User ApprovedByUser { get; set; }

    public virtual User BorrowerUser { get; set; }

    public virtual User ReceivedByUser { get; set; }

    public virtual User RejectedByUser { get; set; }

    public virtual ICollection<ReservationItem> ReservationItems { get; set; } = new List<ReservationItem>();
}
