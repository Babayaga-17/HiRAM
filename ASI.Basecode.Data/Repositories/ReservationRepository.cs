// ASI.Basecode.Data/Repositories/ReservationRepository.cs
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    public class ReservationRepository : BaseRepository, IReservationRepository
    {
        public const string PendingStatus = "Pending";
        public const string ApprovedStatus = "Approved";
        public const string RejectedStatus = "Rejected";
        public const string BorrowedStatus = "Borrowed";

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

        public IQueryable<Reservation> GetReservationsByStatus(string status)
        {
            var query = WithDetails(GetDbSet<Reservation>().AsNoTracking());

            if (!string.IsNullOrWhiteSpace(status) && status != "All")
                query = query.Where(reservation => reservation.Status == status);

            switch (status)
            {
                case PendingStatus: return query.OrderBy(reservation => reservation.RequestedAt);
                case ApprovedStatus: return query.OrderBy(reservation => reservation.StartDateTime);
                case BorrowedStatus: return query.OrderBy(reservation => reservation.ExpectedReturnDateTime);
                default: return query.OrderByDescending(reservation => reservation.RequestedAt);
            }
        }

        public Reservation GetReservationById(int reservationId)
        {
            return WithDetails(GetDbSet<Reservation>().AsNoTracking())
                .FirstOrDefault(reservation => reservation.ReservationId == reservationId);
        }

        public Reservation GetReservationForUpdate(int reservationId)
        {
            return GetDbSet<Reservation>()
                .AsTracking()
                .Include(reservation => reservation.BorrowerUser)
                .Include(reservation => reservation.ReservationItems)
                    .ThenInclude(item => item.Equipment)
                .FirstOrDefault(reservation =>
                    reservation.ReservationId == reservationId);
        }

        public Dictionary<string, int> CountByStatus()
        {
            return GetDbSet<Reservation>()
                .AsNoTracking()
                .GroupBy(reservation => reservation.Status)
                .Select(group => new { Status = group.Key, Count = group.Count() })
                .ToList()
                .ToDictionary(row => row.Status, row => row.Count);
        }

        public bool UserExists(int userId)
        {
            return GetDbSet<User>().AsNoTracking().Any(user => user.UserId == userId);
        }

        public void SaveChanges()
        {
            UnitOfWork.SaveChanges();
        }

        private IQueryable<Reservation> PendingQuery()
        {
            return WithDetails(GetDbSet<Reservation>().AsNoTracking())
                .Where(reservation => reservation.Status == PendingStatus);
        }

        private static IQueryable<Reservation> WithDetails(IQueryable<Reservation> query)
        {
            return query
                .Include(reservation => reservation.BorrowerUser)
                    .ThenInclude(borrower => borrower.User)
                .Include(reservation => reservation.BorrowerUser)
                    .ThenInclude(borrower => borrower.Department)
                .Include(reservation => reservation.ReservationItems)
                    .ThenInclude(item => item.Equipment)
                .Include(reservation => reservation.ApprovedByUser);
        }
    }
}
