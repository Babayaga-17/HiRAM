using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels;

// Read-only notification presentation contracts; recipient filtering is enforced in the repository.
public class NotificationItemViewModel
{
    public long Id { get; set; }
    public string Title { get; set; }
    public string Preview { get; set; }
    public string Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
}

public class NotificationMenuViewModel
{
    public bool IsAvailable { get; set; }
    public int UnreadCount { get; set; }
    public List<NotificationItemViewModel> Items { get; set; } = new();
}

public class NotificationListViewModel : NotificationMenuViewModel
{
    public string Search { get; set; }
    public string Status { get; set; } = "all";
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
    public int PageSize { get; set; } = 10;
}

public class NotificationDetailsViewModel
{
    public bool IsAvailable { get; set; }
    public long Id { get; set; }
    public string Title { get; set; }
    public string Body { get; set; }
    public string Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    // Only a server-generated, authorized local destination may populate this.
    public string EventType { get; set; }
    public int? RelatedUserId { get; set; }
    public int? ReservationId { get; set; }
    public string RelatedUrl { get; set; }
    public string RelatedLabel { get; set; }
}
