using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IAccountRepository
    {
        Account GetByEmail(string email);

        // User and Role Management (WBS 5.1)
        IQueryable<Account> GetAccounts();
        Account GetAccountByUserId(int userId);
        bool EmailExists(string email, int? excludeUserId = null);
        int CountActiveAccountsByRole(byte roleId, int? excludeUserId = null);
        void UpdateAccount(Account account);
    }
}
