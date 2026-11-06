using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models;

public partial class Notification
{
    public long NotificationId { get; set; }

    public int RecipientUserId { get; set; }

    public Guid EventKey { get; set; }

    public string EventType { get; set; }

    public int? RelatedUserId { get; set; }

    public int? ReservationId { get; set; }

    public string Title { get; set; }

    public string Body { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public virtual NotificationEmail NotificationEmail { get; set; }

    public virtual User RecipientUser { get; set; }

    public virtual User RelatedUser { get; set; }

    public virtual Reservation Reservation { get; set; }
}
