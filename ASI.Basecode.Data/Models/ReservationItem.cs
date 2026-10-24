using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models;

public partial class ReservationItem
{
    public int ReservationItemId { get; set; }

    public int ReservationId { get; set; }

    public int EquipmentId { get; set; }

    public string ConditionAtBorrowing { get; set; }

    public string BorrowingRemarks { get; set; }

    public bool IsReturned { get; set; }

    public DateTime? ReturnedAt { get; set; }

    public string ConditionAtReturn { get; set; }

    public string ReturnRemarks { get; set; }

    public string DamageNotes { get; set; }

    public string MissingPartsNotes { get; set; }

    public string CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public string UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Equipment Equipment { get; set; }

    public virtual Reservation Reservation { get; set; }
}
