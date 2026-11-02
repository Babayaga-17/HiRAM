using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    public class ReportRepository : BaseRepository, IReportRepository
    {
        public ReportRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public IQueryable<Reservation> GetBorrowingRecords()
        {
            return GetDbSet<Reservation>()
                .AsNoTracking()
                .Include(reservation => reservation.BorrowerUser).ThenInclude(borrower => borrower.User)
                .Include(reservation => reservation.BorrowerUser).ThenInclude(borrower => borrower.Department)
                .Include(reservation => reservation.ReservationItems).ThenInclude(item => item.Equipment).ThenInclude(equipment => equipment.Category)
                .Include(reservation => reservation.ApprovedByUser)
                .Include(reservation => reservation.RejectedByUser)
                .Include(reservation => reservation.ReceivedByUser);
        }

        public Reservation GetBorrowingRecordById(int reservationId)
        {
            return GetBorrowingRecords().FirstOrDefault(reservation => reservation.ReservationId == reservationId);
        }

        public IQueryable<BorrowerProfile> GetBorrowers()
        {
            return GetDbSet<BorrowerProfile>().AsNoTracking().Include(borrower => borrower.User);
        }

        public IQueryable<Equipment> GetEquipments()
        {
            return GetDbSet<Equipment>().AsNoTracking();
        }
    }
}