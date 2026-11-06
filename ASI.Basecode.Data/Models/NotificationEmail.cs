using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models;

public partial class NotificationEmail
{
    public long NotificationEmailId { get; set; }

    public long NotificationId { get; set; }

    public string RecipientEmail { get; set; }

    public string Subject { get; set; }

    public string Body { get; set; }

    public string DeliveryStatus { get; set; }

    public int AttemptCount { get; set; }

    public DateTime NextAttemptAt { get; set; }

    public Guid? LeaseToken { get; set; }

    public DateTime? LockedUntil { get; set; }

    public DateTime? SentAt { get; set; }

    public string LastError { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Notification Notification { get; set; }
}
