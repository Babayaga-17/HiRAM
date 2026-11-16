using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models;

public partial class User
{
    public int UserId { get; set; }

    public string UserCode { get; set; }

    public string FirstName { get; set; }

    public string MiddleName { get; set; }

    public string LastName { get; set; }

    public string Suffix { get; set; }

    public string ContactNumber { get; set; }

    public string ProfilePhotoStoragePath { get; set; }

    public string CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public string UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Account Account { get; set; }

    public virtual ICollection<BorrowerProfile> BorrowerProfileSuspendedByUsers { get; set; } = new List<BorrowerProfile>();

    public virtual BorrowerProfile BorrowerProfileUser { get; set; }

    public virtual ICollection<MaintenanceRecord> MaintenanceRecordCompletedByUsers { get; set; } = new List<MaintenanceRecord>();

    public virtual ICollection<MaintenanceRecord> MaintenanceRecordReportedByUsers { get; set; } = new List<MaintenanceRecord>();

    public virtual ICollection<Notification> NotificationRecipientUsers { get; set; } = new List<Notification>();

    public virtual ICollection<Notification> NotificationRelatedUsers { get; set; } = new List<Notification>();

    public virtual ICollection<Reservation> ReservationApprovedByUsers { get; set; } = new List<Reservation>();

    public virtual ICollection<Reservation> ReservationReceivedByUsers { get; set; } = new List<Reservation>();

    public virtual ICollection<Reservation> ReservationRejectedByUsers { get; set; } = new List<Reservation>();
}
