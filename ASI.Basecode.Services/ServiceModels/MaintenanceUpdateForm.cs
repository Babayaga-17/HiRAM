using System;
using System.ComponentModel.DataAnnotations;
namespace ASI.Basecode.Services.ServiceModels;
public class MaintenanceUpdateForm
{
    public int Id { get; set; }
    [Required] public string Operation { get; set; }
    public DateTime? ExpectedCompletionDateTime { get; set; }
    [StringLength(500)] public string Remarks { get; set; }
}
