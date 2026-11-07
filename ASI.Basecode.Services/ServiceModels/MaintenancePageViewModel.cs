using System.Collections.Generic;
using ASI.Basecode.Data.Models;
namespace ASI.Basecode.Services.ServiceModels;

// Form options and a paged maintenance list.
public class MaintenancePageViewModel
{
    public MaintenanceForm Form { get; set; } = new();
    public string Search { get; set; }
    public string Status { get; set; }
    public int Page { get; set; } = 1;
    public int TotalCount { get; set; }
    public int TotalPages => System.Math.Max(1, (TotalCount + 19) / 20);
    public int? SelectedEquipmentId { get; set; }
    public List<EquipmentListItem> Equipment { get; set; } = new();
    public List<MaintenanceRecord> Records { get; set; } = new();
    public bool RecordsAvailable { get; set; }
    public string EquipmentError { get; set; }
}


