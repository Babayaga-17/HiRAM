using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces;

public interface IEquipmentReservationService
{
    (string ReservationNumber, string Error) Submit(ReservationFormModel form, int borrowerUserId);
}
