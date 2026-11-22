using ASI.Basecode.Data.Models;
using System.Linq;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IReportRepository
    {
        IQueryable<Reservation> GetBorrowingRecords();
        Reservation GetBorrowingRecordById(int reservationId);
        IQueryable<BorrowerProfile> GetBorrowers();
        IQueryable<Equipment> GetEquipments();
    }
}