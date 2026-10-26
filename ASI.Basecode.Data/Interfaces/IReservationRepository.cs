using ASI.Basecode.Data.Models;
using System.Linq;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IReservationRepository
    {
        IQueryable<Reservation> GetPendingReservations();
        Reservation GetPendingReservationById(int reservationId);
        int CountPendingReservations();
    }
}
