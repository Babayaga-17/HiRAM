using System;
using System.Collections.Generic;
using System.Linq;
using ASI.Basecode.Resources.Constants;
namespace ASI.Basecode.Services.ServiceModels;

public class DashboardReservation
{
    public int ReservationId { get; set; }
    public string ReservationNumber { get; set; }
    public string EquipmentSummary { get; set; }
    public string Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime PickupAt { get; set; }
    public DateTime ReturnBy { get; set; }
    public bool IsOverdue { get; set; }
    public bool IsOutstanding => Status == ReservationStatuses.Borrowed || Status == ReservationStatuses.Overdue;
}
public class BorrowerDashboardViewModel
{
    // Null means unavailable; an empty list means a successful read with no reservations.
    public List<DashboardReservation> Reservations { get; set; }
    public bool ProfileLoaded { get; set; }
    public string EligibilityStatus { get; set; }
    public string BorrowerType { get; set; }
    public int? Count(string status) => Reservations?.Count(r => r.Status == status);
    public int? OutstandingCount => Reservations?.Count(r => r.IsOutstanding);
    public int? OverdueCount => Reservations?.Count(r => r.IsOverdue);
    public IEnumerable<DashboardReservation> Loans => Reservations?.Where(r => r.IsOutstanding).OrderBy(r => r.ReturnBy).ThenBy(r => r.ReservationId).Take(4) ?? Enumerable.Empty<DashboardReservation>();
    public IEnumerable<DashboardReservation> Pickups => Reservations?.Where(r => r.Status == ReservationStatuses.Approved).OrderBy(r => r.PickupAt).ThenBy(r => r.ReservationId).Take(3) ?? Enumerable.Empty<DashboardReservation>();
    public IEnumerable<DashboardReservation> Recent => Reservations?.Take(4) ?? Enumerable.Empty<DashboardReservation>();
}
public class CustodianDashboardViewModel
{
    public ReservationListViewModel Pending { get; set; }
    public ReservationListViewModel Approved { get; set; }
    public ReservationListViewModel Borrowed { get; set; }
    public BorrowingReportViewModel Overdue { get; set; }
    public EquipmentIndexViewModel Equipment { get; set; }
    public MaintenancePageViewModel ScheduledMaintenance { get; set; }
    public MaintenancePageViewModel OngoingMaintenance { get; set; }
    public int? Count(string status)
    {
        var source = status == ReservationStatuses.Approved ? Approved
            : status == ReservationStatuses.Borrowed || status == ReservationStatuses.Overdue ? Borrowed : Pending;
        return source == null ? null : source.Counts.TryGetValue(status, out var count) ? count : 0;
    }
    public int? OutstandingCount => Count(ReservationStatuses.Borrowed) + Count(ReservationStatuses.Overdue);
}
