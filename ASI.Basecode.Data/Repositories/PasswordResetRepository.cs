using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using System;
using System.Linq;

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

        public void Save() => UnitOfWork.SaveChanges();
    }
}