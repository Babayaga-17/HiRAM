using System;
using System.Collections.Generic;
using System.Linq;
namespace ASI.Basecode.Services.ServiceModels;

public class AdminDashboardViewModel
{
    public List<UserAccountListItem> Accounts { get; set; }
    public EquipmentIndexViewModel Equipment { get; set; }
    public ReservationListViewModel Reservations { get; set; }
    public BorrowingReportViewModel Overdue { get; set; }
    public BorrowerPageViewModel Borrowers { get; set; }
    public MaintenancePageViewModel ScheduledMaintenance { get; set; }
    public MaintenancePageViewModel OngoingMaintenance { get; set; }
    public DateTime LoadedAt { get; set; } = DateTime.Now;
    public int? PendingRegistrations => Accounts?.Count(a => a.AccountStatus == Resources.Constants.AccountStatuses.PendingApproval);
    public IEnumerable<UserAccountListItem> RegistrationPreview => Accounts?.Where(a => a.AccountStatus == Resources.Constants.AccountStatuses.PendingApproval).Take(4) ?? Enumerable.Empty<UserAccountListItem>();
    public int? ReservationCount(string status) => Reservations == null ? null : Reservations.Counts.TryGetValue(status, out var count) ? count : 0;
}
