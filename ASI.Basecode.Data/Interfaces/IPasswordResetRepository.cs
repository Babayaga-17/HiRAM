using System;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IPasswordResetRepository
    {
        Account GetAccountByEmail(string email);
        void AddToken(PasswordResetToken token);
        bool TryAddToken(PasswordResetToken token, DateTime cooldownCutoffUtc);
        PasswordResetToken GetLatestActiveToken(int accountId);
        PasswordResetToken GetLatestToken(int accountId);
        void Save();
    }
}