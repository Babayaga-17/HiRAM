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
    public class AccountRepository : BaseRepository, IAccountRepository
    {
        public AccountRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public Account GetByEmail(string email)
        {
            return GetDbSet<Account>().Include(account => account.User)
                                      .Include(account => account.Role)
                                      .FirstOrDefault(account => account.Email == email);
        }

        // User and Role Management (WBS 5.1)

        public IQueryable<Account> GetAccounts()
        {
            return GetDbSet<Account>().AsNoTracking();
        }

        public Account GetAccountByUserId(int userId)
        {
            return GetDbSet<Account>().AsTracking()
                                      .Include(account => account.User)
                                      .Include(account => account.Role)
                                      .FirstOrDefault(account => account.UserId == userId);
        }

        public bool EmailExists(string email, int? excludeUserId = null)
        {
            var query = GetDbSet<Account>().Where(account => account.Email == email);

            if (excludeUserId.HasValue)
            {
                query = query.Where(account => account.UserId != excludeUserId.Value);
            }

            return query.Any();
        }

        public int CountActiveAccountsByRole(byte roleId, int? excludeUserId = null)
        {
            var query = GetDbSet<Account>().Where(account => account.RoleId == roleId && account.AccountStatus == "Active");

            if (excludeUserId.HasValue)
            {
                query = query.Where(account => account.UserId != excludeUserId.Value);
            }

            return query.Count();
        }

        public void UpdateAccount(Account account)
        {
            // Accounts loaded with GetAccountByUserId are tracked, so only the changed columns
            // (including changes to account.User) are saved.
            if (Context.Entry(account).State == EntityState.Detached)
            {
                GetDbSet<Account>().Update(account);
            }

            UnitOfWork.SaveChanges();
        }
    }
}
