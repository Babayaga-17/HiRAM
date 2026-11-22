using System;
using System.Collections.Generic;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Services.ServiceModels.Reservation;

public class MyReservationPageViewModel
{
    public static IReadOnlyList<string> StatusOptions { get; } = ReservationStatuses.Values;
    public List<MyReservationCard> Items { get; set; } = new();
    public string Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 5;
    public int TotalCount { get; set; }
    public int FilteredCount { get; set; }
    public bool IsAvailable { get; set; } = true;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(FilteredCount / (double)PageSize));
    public int FirstItem => FilteredCount == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int LastItem => Math.Min(Page * PageSize, FilteredCount);
}

public class MyReservationCard
{
    public int ReservationId { get; set; }
    public string ReservationNumber { get; set; }
    public string Status { get; set; }
    public List<string> EquipmentNames { get; set; } = new();
    public DateTime PickupAt { get; set; }
    public DateTime ReturnBy { get; set; }
}
