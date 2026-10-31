// ASI.Basecode.Data/Interfaces/IReservationRepository.cs
using ASI.Basecode.Data.Models;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IReservationRepository
    {
        IQueryable<Reservation> GetPendingReservations();
        Reservation GetPendingReservationById(int reservationId);
        int CountPendingReservations();

        IQueryable<Reservation> GetReservationsByStatus(string status);   // null / "All" = every status
        Reservation GetReservationById(int reservationId);                // read-only, with details
        Reservation GetReservationForUpdate(int reservationId);           // tracked, to approve / reject / release
        Dictionary<string, int> CountByStatus();
        bool UserExists(int userId);
        void SaveChanges();
    }
}
