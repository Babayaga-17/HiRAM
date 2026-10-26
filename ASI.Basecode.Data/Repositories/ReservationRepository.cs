using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    public class ReservationRepository : BaseRepository, IReservationRepository
    {
        public const string PendingStatus = "Pending";

        public ReservationRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public IQueryable<Reservation> GetPendingReservations()
        {
            return PendingQuery()
                .OrderBy(reservation => reservation.RequestedAt);
        }

        public Reservation GetPendingReservationById(int reservationId)
        {
            return PendingQuery()
                .FirstOrDefault(reservation => reservation.ReservationId == reservationId);
        }

        public int CountPendingReservations()
        {
            return GetDbSet<Reservation>()
                .AsNoTracking()
                .Count(reservation => reservation.Status == PendingStatus);
        }

        private IQueryable<Reservation> PendingQuery()
        {
            return GetDbSet<Reservation>()
                .AsNoTracking()
                .Where(reservation => reservation.Status == PendingStatus)
                .Include(reservation => reservation.BorrowerUser)
                    .ThenInclude(borrower => borrower.User)
                .Include(reservation => reservation.BorrowerUser)
                    .ThenInclude(borrower => borrower.Department)
                .Include(reservation => reservation.ReservationItems)
                    .ThenInclude(item => item.Equipment);
        }
    }
}
