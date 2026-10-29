using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels;

public class ReservationCancelFormModel
{
    [StringLength(500, ErrorMessage = "Keep the cancellation reason within 500 characters.")]
    public string Reason { get; set; }

    [Required(ErrorMessage = "Reload the reservation before cancelling.")]
    public string RowVersion { get; set; }
}
