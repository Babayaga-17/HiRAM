// ASI.Basecode.Data/Interfaces/IReservationRepository.cs
using ASI.Basecode.Data.Models;
using System.Collections.Generic;
using System.Linq;
using System;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IReservationRepository
    {
        List<Reservation> GetBorrowerDashboardReservations(int borrowerUserId, DateTime now);

        IQueryable<Reservation> GetPendingReservations();
        Reservation GetPendingReservationById(int reservationId);
        int CountPendingReservations();

        IQueryable<Reservation> GetReservationsByStatus(string status);   // null / "All" = every status
        Reservation GetReservationById(int reservationId);                // read-only, with details
        Reservation GetReservationForUpdate(int reservationId);           // tracked, to approve / reject / release
        Dictionary<string, int> CountByStatus();
        bool UserExists(int userId);
        void SaveChanges();
        (bool Ok, string Message) Review(int id, Func<Reservation, (bool Ok, string Message)> applyChanges);
    }
}
