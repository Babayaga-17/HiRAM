// ASI.Basecode.Data/Repositories/ReservationRepository.cs
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System;
using ASI.Basecode.Data.Exceptions;

namespace ASI.Basecode.Data.Repositories
{
    public class ReservationRepository : BaseRepository, IReservationRepository
    {
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
                .Count(reservation => reservation.Status == ReservationStatuses.Pending);
        }

        public IQueryable<Reservation> GetReservationsByStatus(string status)
        {
            var query = WithDetails(GetDbSet<Reservation>().AsNoTracking());

            if (!string.IsNullOrWhiteSpace(status) && status != ReservationFilters.All)
                query = query.Where(reservation => reservation.Status == status);

            switch (status)
            {
                case ReservationStatuses.Pending: return query.OrderBy(reservation => reservation.RequestedAt);
                case ReservationStatuses.Approved: return query.OrderBy(reservation => reservation.StartDateTime);
                case ReservationStatuses.Borrowed: return query.OrderBy(reservation => reservation.ExpectedReturnDateTime);
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

        public (bool Ok, string Message) Review(int id, Func<Reservation, (bool Ok, string Message)> applyChanges)
        {
            using var transaction = Context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
            try
            {
                var reservation = GetDbSet<Reservation>()
                    .FromSqlInterpolated($"SELECT * FROM dbo.Reservations WITH (UPDLOCK, HOLDLOCK) WHERE reservation_id = {id}")
                    .AsTracking().Include(r => r.BorrowerUser).SingleOrDefault();
                var result = applyChanges(reservation);
                if (!result.Ok) return result;
                UnitOfWork.SaveChanges();
                transaction.Commit();
                return result;
            }
            catch (DbUpdateConcurrencyException exception) { throw new ReservationConcurrencyException(exception); }
        }

        private IQueryable<Reservation> PendingQuery()
        {
            return WithDetails(GetDbSet<Reservation>().AsNoTracking())
                .Where(reservation => reservation.Status == ReservationStatuses.Pending);
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
