using ASI.Basecode.Resources.Constants;
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

        public Account GetAccountByEmail(string email)
        {
            return GetDbSet<Account>().Include(account => account.User)
                                      .Include(account => account.Role)
                                      .FirstOrDefault(account => account.Email == email);
        }

        public async Task UpdateLastLoginAtAsync(int accountId, DateTime loginAtUtc)
        {
            await GetDbSet<Account>()
                .Where(account => account.AccountId == accountId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(account => account.LastLoginAt, (DateTime?)loginAtUtc));
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
            var query = GetDbSet<Account>().Where(account => account.RoleId == roleId && account.AccountStatus == AccountStatuses.Active);

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

        public bool UpdateRegistration(int userId, Action<Account> applyChanges)
        {
            using var transaction = Context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
            // Serialize reviews of the same account before the service checks its state.
            var account = GetDbSet<Account>()
                .FromSqlInterpolated($"SELECT * FROM dbo.Accounts WITH (UPDLOCK, HOLDLOCK) WHERE user_id = {userId}")
                .AsTracking()
                .Include(a => a.Role)
                .Include(a => a.User).ThenInclude(u => u.BorrowerProfileUser)
                .SingleOrDefault();
            if (account == null) return false;

            applyChanges(account);
            UnitOfWork.SaveChanges();
            transaction.Commit();
            return true;
        }

        public Task<Dictionary<string, int>> CountAccountsByStatusAsync()
        {
            return GetDbSet<Account>()
                .AsNoTracking()
                .GroupBy(account => account.AccountStatus)
                .Select(group => new
                {
                    Status = group.Key,
                    Count = group.Count()
                })
                .ToDictionaryAsync(row => row.Status, row => row.Count);
        }

        public Task<List<Account>> GetPendingRegistrationPreviewAsync()
        {
            return GetDbSet<Account>()
                .AsNoTracking()
                .Where(account =>
                    account.AccountStatus == AccountStatuses.PendingApproval)
                .OrderBy(account => account.User.LastName)
                .ThenBy(account => account.User.FirstName)
                .ThenBy(account => account.UserId)
                .Take(4)
                .Include(account => account.User)
                .ToListAsync();
        }
    }
}
