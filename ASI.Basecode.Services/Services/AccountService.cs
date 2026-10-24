using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ASI.Basecode.Resources.Constants.Enums;

namespace ASI.Basecode.Services.Services
{
    public class AccountService : IAccountService
    {
        private readonly IAccountRepository _accountRepository;

        public AccountService(IAccountRepository accountRepository)
        {
            _accountRepository = accountRepository;
        }

        public LoginResult AuthenticateAccount(string email, string password, ref Account account)
        {
            account = _accountRepository.GetByEmail(email);

            if (account == null)
            {
                return LoginResult.Failed;
            }

            if (!string.Equals(account.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return LoginResult.Failed;
            }

            if (account.LockoutEndAt.HasValue && account.LockoutEndAt.Value > DateTime.Now)
            {
                return LoginResult.Failed;
            }

            bool passwordMatches = PasswordManager.EncryptPassword(password).Equals(account.PasswordHash);

            return passwordMatches ? LoginResult.Success : LoginResult.Failed;


        }
    }
}
