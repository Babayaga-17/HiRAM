using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels.Reservation;

public class ReservationViewModel
{
    public int ReservationId { get; set; }
    public string ReservationNumber { get; set; }
    public string Status { get; set; }
    public string Purpose { get; set; }

    public DateTime RequestedAt { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime ExpectedReturnDateTime { get; set; }

    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? BorrowedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public string RejectionReason { get; set; }
    public string CancellationReason { get; set; }

    public List<EquipmentListItem> Equipment { get; set; } = new();

    public byte[] RowVersion { get; set; }
}
