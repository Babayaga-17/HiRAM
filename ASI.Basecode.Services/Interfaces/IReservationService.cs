using ASI.Basecode.Services.ServiceModels;
using System.Collections.Generic;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IReservationService
    {
        List<PendingReservationListItem> GetPendingReservations();
        PendingReservationDetailsViewModel GetPendingReservationById(int reservationId);
        int CountPendingReservations();
    }
}
