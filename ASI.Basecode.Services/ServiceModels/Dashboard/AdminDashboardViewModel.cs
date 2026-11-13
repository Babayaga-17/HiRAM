using ASI.Basecode.Services.ServiceModels;
using System.Collections.Generic;

public class AdminDashboardViewModel
{
    public int? TotalUsers { get; set; }
    public int? PendingRegistrationCount { get; set; }
    public int? TotalEquipment { get; set; }

    public List<UserAccountListItem> RegistrationPreview { get; set; }
}