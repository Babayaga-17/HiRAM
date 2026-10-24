using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IPasswordResetRepository
    {
        Account GetAccountByEmail(string email);
        void AddToken(PasswordResetToken token);
        PasswordResetToken GetLatestActiveToken(int accountId);
        void Save();
    }
}