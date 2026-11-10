// ASI.Basecode.Data/Repositories/ReservationRepository.cs
using ASI.Basecode.Data.Exceptions;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static ASI.Basecode.Resources.Constants.Enums;

namespace ASI.Basecode.Data.Repositories
{
    public class ReservationRepository : BaseRepository, IReservationRepository
    {
        public ReservationRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        // All list queries are scoped before counts, related-data loading or materialization.
        private IQueryable<Reservation> BorrowerListQuery(int borrowerUserId, string status, DateTime now)
        {
            if (borrowerUserId <= 0) // ensures no negative or zero borrowerUserId is passed, which would be invalid
                throw new ArgumentOutOfRangeException(nameof(borrowerUserId));

            var query = GetDbSet<Reservation>()
                .AsNoTracking() // Not tracked for performance since we are only reading data
                .Where(r => r.BorrowerUserId == borrowerUserId);

            if (status == ReservationStatuses.Overdue)
                return query.Where(r => (r.Status == ReservationStatuses.Borrowed || r.Status == ReservationStatuses.Overdue)
                    && r.ExpectedReturnDateTime < now);
            if (status == ReservationStatuses.Borrowed)
                return query.Where(r => (r.Status == ReservationStatuses.Borrowed || r.Status == ReservationStatuses.Overdue)
                    && r.ExpectedReturnDateTime >= now);

            return string.IsNullOrEmpty(status) ? query : query.Where(r => r.Status == status);
        }

        public Task<int> CountBorrowerReservationsAsync(int borrowerUserId, string status, DateTime now) => BorrowerListQuery(borrowerUserId, status, now).CountAsync();

        public Task<List<Reservation>> GetBorrowerReservationPageAsync(int borrowerUserId, string status, DateTime now, int skip, int take)
        {
            if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip)); // ensures no negative skip value is passed, which would be invalid
            if (take != 5 && take != 10 && take != 20) throw new ArgumentOutOfRangeException(nameof(take)); // ensures only valid take values are passed

            return BorrowerListQuery(borrowerUserId, status, now)
                .OrderByDescending(r => r.RequestedAt)
                .ThenByDescending(r => r.ReservationId)
                .Skip(skip).Take(take)
                .Include(r => r.ReservationItems).ThenInclude(item => item.Equipment)
                .ToListAsync();
        }

        public Task<List<Reservation>> GetBorrowerDashboardReservationsAsync(int borrowerUserId, DateTime now)
        {
            if (borrowerUserId <= 0) // ensures no negative or zero borrowerUserId is passed, which would be invalid
                throw new ArgumentOutOfRangeException(nameof(borrowerUserId));

            return BorrowerListQuery(borrowerUserId, null, now)
                .Include(r => r.ReservationItems)
                    .ThenInclude(item => item.Equipment)
                .Where(r => r.BorrowerUserId == borrowerUserId)
                .Where(r =>
                    r.Status == ReservationStatuses.Approved ||
                    r.Status == ReservationStatuses.Borrowed ||
                    r.Status == ReservationStatuses.Overdue)
                .OrderByDescending(r =>
                    r.Status != ReservationStatuses.Approved &&
                    r.ExpectedReturnDateTime < now)
                .ThenByDescending(r =>
                    r.Status == ReservationStatuses.Approved)
                .ThenBy(r => r.Status == ReservationStatuses.Approved
                    ? r.StartDateTime
                    : r.ExpectedReturnDateTime)
                .ThenBy(r => r.ReservationId)
                .Take(3)
                .ToListAsync();
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
