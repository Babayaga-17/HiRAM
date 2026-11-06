using System;
using System.Collections.Generic;

namespace ASI.Basecode.WebApp.Models;

// Presentation contracts only. Retrieval and recipient authorization will be connected separately.
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
    public string RelatedUrl { get; set; }
    public string RelatedLabel { get; set; }
}
