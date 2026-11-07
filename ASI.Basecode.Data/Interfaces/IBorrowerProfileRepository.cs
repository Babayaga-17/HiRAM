using ASI.Basecode.Data.Models;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IBorrowerProfileRepository
    {
        List<Department> GetActiveDepartments();
        int CountProfiles(string eligibilityStatus = null);
        int CountMatchingProfiles(string search, string status);
        List<BorrowerProfile> GetPage(string search, string status, int skip, int take);
        BorrowerProfile GetForHistory(int userId);
        List<Reservation> GetReservations(int userId);
        BorrowerProfile GetForUpdate(int userId);
        bool HasReservations(int userId);
        void SaveChanges();
        void Delete(BorrowerProfile profile);
    }
}
