using System;
using System.ComponentModel.DataAnnotations;
namespace ASI.Basecode.Services.ServiceModels;
public class MaintenanceForm
{
    [Range(1, int.MaxValue)] public int EquipmentId { get; set; }
    [Required, StringLength(50)] public string MaintenanceType { get; set; }
    [Required, StringLength(500)] public string Description { get; set; }
    public DateTime StartDateTime { get; set; } = DateTime.Now;
    public DateTime? EndDateTime { get; set; }
}
