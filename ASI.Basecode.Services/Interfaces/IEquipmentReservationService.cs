using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.ServiceModels.Reservation;
using System.Collections.Generic;

namespace ASI.Basecode.Services.Interfaces;

public interface IEquipmentReservationService
{
    (string ReservationNumber, string Error) Submit(ReservationFormModel form, int borrowerUserId);
    List<ReservationViewModel> GetPendingReservations(int borrowerUserId);
    ReservationViewModel GetReservationDetails( int reservationId, int borrowerUserId);
    string Edit(int reservationId, ReservationEditFormModel form, int borrowerUserId);
    string Cancel(int reservationId, ReservationCancelFormModel form, int borrowerUserId);
}
