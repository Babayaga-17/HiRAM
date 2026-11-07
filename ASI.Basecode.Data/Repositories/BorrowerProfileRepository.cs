using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    public class BorrowerProfileRepository : BaseRepository, IBorrowerProfileRepository
    {
        public BorrowerProfileRepository(IUnitOfWork unitOfWork) : base(unitOfWork) { }

        public List<Department> GetActiveDepartments() => GetDbSet<Department>()
            .AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.DepartmentName).ToList();

        public int CountProfiles(string eligibilityStatus = null)
        {
            var query = GetDbSet<BorrowerProfile>().AsNoTracking();
            if (eligibilityStatus != null)
                query = query.Where(b => b.EligibilityStatus == eligibilityStatus);
            return query.Count();
        }

        private IQueryable<BorrowerProfile> MatchingProfiles(string search, string status)
        {
            var query = GetDbSet<BorrowerProfile>().AsNoTracking();
            if (status != null)
                query = query.Where(b => b.EligibilityStatus == status);
            if (!string.IsNullOrEmpty(search))
                query = query.Where(b => b.User.FirstName.Contains(search) || b.User.LastName.Contains(search)
                    || b.User.UserCode.Contains(search) || b.Program.Contains(search)
                    || b.Department.DepartmentName.Contains(search));
            return query;
        }

        public int CountMatchingProfiles(string search, string status) => MatchingProfiles(search, status).Count();

        public List<BorrowerProfile> GetPage(string search, string status, int skip, int take) =>
            MatchingProfiles(search, status).Include(b => b.User).Include(b => b.Department)
                .OrderBy(b => b.User.LastName).ThenBy(b => b.User.FirstName).ThenBy(b => b.UserId)
                .Skip(skip).Take(take).ToList();

        public BorrowerProfile GetForHistory(int userId) => GetDbSet<BorrowerProfile>()
            .AsNoTracking().Include(b => b.User).FirstOrDefault(b => b.UserId == userId);

        public List<Reservation> GetReservations(int userId) => GetDbSet<Reservation>()
            .AsNoTracking().Where(r => r.BorrowerUserId == userId)
            .Include(r => r.ReservationItems).ThenInclude(i => i.Equipment)
            .OrderByDescending(r => r.RequestedAt).ToList();

        public BorrowerProfile GetForUpdate(int userId) => GetDbSet<BorrowerProfile>()
            .AsTracking().FirstOrDefault(b => b.UserId == userId);

        public bool HasReservations(int userId) => GetDbSet<Reservation>().Any(r => r.BorrowerUserId == userId);

        public void SaveChanges() => UnitOfWork.SaveChanges();

        public void Delete(BorrowerProfile profile)
        {
            GetDbSet<BorrowerProfile>().Remove(profile);
            UnitOfWork.SaveChanges();
        }
    }
}
