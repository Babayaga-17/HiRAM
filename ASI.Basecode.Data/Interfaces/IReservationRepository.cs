// ASI.Basecode.Data/Interfaces/IReservationRepository.cs
using ASI.Basecode.Data.Models;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IReservationRepository
    {
        Task<int> CountBorrowerReservationsAsync(int borrowerUserId, string status, DateTime now);
        Task<List<Reservation>> GetBorrowerReservationPageAsync(int borrowerUserId, string status, DateTime now, int skip, int take);
        Task<List<Reservation>> GetBorrowerDashboardReservationsAsync(int borrowerUserId, DateTime now);
        Task<List<Reservation>> GetPendingReservationPreviewAsync();
        public BorrowerProfile GetBorrowerProfile(int borrowerUserId);
        public void AddReservation(Reservation reservation);
        public Equipment GetEquipment(int equipmentId);
        public string ExecuteInTransaction(Func<string> operation);
        public Reservation GetReservationDetails(int reservationId, int borrowerUserId);
        public Reservation GetReservationForUpdate(int reservationId, int borrowerUserId);
        public void RemoveReservationItems(IEnumerable<ReservationItem> items);





        IQueryable<Reservation> GetPendingReservations();
        Reservation GetPendingReservationById(int reservationId);
        int CountPendingReservations();

        Task<List<Reservation>> GetReservationsByStatusAsync(string status);   // null / "All" = every status
        Reservation GetReservationById(int reservationId);                // read-only, with details
        Reservation GetReservationForUpdate(int reservationId);           // tracked, to approve / reject / release
        Task<Dictionary<string, int>> CountByStatus();
        bool UserExists(int userId);
        void SaveChanges();
        (bool Ok, string Message) Review(int id, Func<Reservation, (bool Ok, string Message)> applyChanges);
    }
}
