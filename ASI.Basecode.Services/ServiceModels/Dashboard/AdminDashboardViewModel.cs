using System;
using System.Collections.Generic;
using System.Linq;
namespace ASI.Basecode.Services.ServiceModels.Dashboard;

public class AdminDashboardViewModel
{
    public List<UserAccountListItem> Accounts { get; set; }
    public EquipmentIndexViewModel Equipment { get; set; }
    public DateTime LoadedAt { get; set; } = DateTime.Now;
    public int? PendingRegistrations => Accounts?.Count(a => a.AccountStatus == Resources.Constants.AccountStatuses.PendingApproval);
    public IEnumerable<UserAccountListItem> RegistrationPreview => Accounts?.Where(a => a.AccountStatus == Resources.Constants.AccountStatuses.PendingApproval).Take(4) ?? Enumerable.Empty<UserAccountListItem>();
}
