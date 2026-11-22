using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories
{
    public class PasswordResetRepository : BaseRepository, IPasswordResetRepository
    {
        public PasswordResetRepository(IUnitOfWork unitOfWork) : base(unitOfWork) { }

        public Account GetAccountByEmail(string email)
        {
            return this.GetDbSet<Account>().FirstOrDefault(a => a.Email == email);
        }

        public void AddToken(PasswordResetToken token)
        {
            this.GetDbSet<PasswordResetToken>().Add(token);
            UnitOfWork.SaveChanges();
        }

        public PasswordResetToken GetLatestActiveToken(int accountId)
        {
            return this.GetDbSet<PasswordResetToken>()
                .Where(t => t.AccountId == accountId && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefault();
        }
        public PasswordResetToken GetLatestToken(int accountId)
        {
            return this.GetDbSet<PasswordResetToken>()
                .Where(t => t.AccountId == accountId)
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.TokenId)
                .FirstOrDefault();
        }

        public void Save() => UnitOfWork.SaveChanges();

        public bool TryAddToken(PasswordResetToken token, DateTime cooldownCutoffUtc)
        {
            using var transaction = Context.Database.BeginTransaction();

            // Serialize reset-code issuance for this account.
            var account = GetDbSet<Account>()
                .FromSqlInterpolated($@"
            SELECT *
            FROM dbo.Accounts WITH (UPDLOCK, HOLDLOCK)
            WHERE account_id = {token.AccountId}")
                .AsNoTracking()
                .ToList()
                .SingleOrDefault();

            if (account == null)
                throw new InvalidOperationException("Reset account no longer exists.");

            var recentlyIssued = GetDbSet<PasswordResetToken>()
                .AsNoTracking()
                .Any(t => t.AccountId == token.AccountId
                       && t.CreatedAt > cooldownCutoffUtc);

            if (recentlyIssued)
                return false;

            GetDbSet<PasswordResetToken>().Add(token);
            UnitOfWork.SaveChanges();

            transaction.Commit();
            return true;
        }
    }
}