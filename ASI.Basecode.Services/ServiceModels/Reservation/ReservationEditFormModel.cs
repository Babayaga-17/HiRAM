using ASI.Basecode.Services.ServiceModels.Reservation;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels;

public class ReservationEditFormModel : ReservationFormModel
{
    [Required(ErrorMessage = "Reload the reservation before editing.")]
    public string RowVersion { get; set; }
}