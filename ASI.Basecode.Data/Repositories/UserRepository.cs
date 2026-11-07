using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Repositories
{
    public class UserRepository : BaseRepository, IUserRepository
    {
        public UserRepository(IUnitOfWork unitOfWork) : base(unitOfWork) 
        {

        }

        public IQueryable<User> GetUsers()
        {
            return this.GetDbSet<User>();
        }

        public bool UserExists(int userId)
        {
            return this.GetDbSet<Account>().Any(x => x.UserId == userId);
        }

        public void AddUser(User user)
        {
            this.GetDbSet<User>().Add(user);
            UnitOfWork.SaveChanges();
        }

        public User GetProfileByUserId(int userId)
        {
            return GetDbSet<User>()
                .AsNoTracking()
                .Include(user => user.Account)
                    .ThenInclude(account => account.Role)
                .Include(user => user.BorrowerProfileUser)
                    .ThenInclude(profile => profile.Department)
                .FirstOrDefault(user => user.UserId == userId);
        }

        public bool UserCodeExists(string userCode, int? excludeUserId = null)
        {
            var query = this.GetDbSet<User>().Where(user => user.UserCode == userCode);

            if (excludeUserId.HasValue)
            {
                query = query.Where(user => user.UserId != excludeUserId.Value);
            }

            return query.Any();
        }
    }
}
