using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ServiceModels;
using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static ASI.Basecode.Resources.Constants.Enums;

namespace ASI.Basecode.Services.Services
{
    public class AccountService : IAccountService
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IUserRepository _userRepository;
        private readonly IProfilePhotoService _profilePhotoService;

        public AccountService(IAccountRepository accountRepository,
                              IUserRepository userRepository,
                              IProfilePhotoService profilePhotoService)
        {
            _accountRepository = accountRepository;
            _userRepository = userRepository;
            _profilePhotoService = profilePhotoService;
        }

        public LoginResult AuthenticateAccount(
    string email, string password, ref Account account)
        {
            account = _accountRepository.GetByEmail(email);

            if (account == null)
                return LoginResult.Failed;

            if (account.LockoutEndAt.HasValue && account.LockoutEndAt.Value > DateTime.Now)
                return LoginResult.Failed;

            bool passwordMatches = PasswordManager
                .EncryptPassword(password)
                .Equals(account.PasswordHash);

            if (!passwordMatches)
                return LoginResult.Failed;

            if (string.Equals(account.AccountStatus, AccountStatuses.PendingApproval, StringComparison.OrdinalIgnoreCase))
                return LoginResult.PendingApproval;

            if (!string.Equals(account.AccountStatus, AccountStatuses.Active, StringComparison.OrdinalIgnoreCase))
                return LoginResult.Failed;

            return LoginResult.Success;
        }

        public Task RecordSuccessfulLoginAsync(int accountId)
        {
            return _accountRepository.UpdateLastLoginAtAsync(accountId, DateTime.UtcNow);
        }

        public async Task<MyProfileViewModel> GetProfileAsync(int userId, CancellationToken cancellationToken = default)
        {
            var user = _userRepository.GetProfileByUserId(userId);

            if (user == null)
                return null;

            var model = new MyProfileViewModel
            {
                UserCode = user.UserCode,
                FullName = string.Join(" ", new[]
                {
                    user.FirstName,
                    user.MiddleName,
                    user.LastName,
                    user.Suffix
                }.Where(part => !string.IsNullOrWhiteSpace(part))),
                Email = user.Account?.Email,
                ContactNumber = user.ContactNumber,
                RoleName = user.Account?.Role?.RoleName,
                AccountStatus = user.Account?.AccountStatus,
                HasBorrowerProfile = user.BorrowerProfileUser != null,
                BorrowerType = user.BorrowerProfileUser?.BorrowerType,
                DepartmentName = user.BorrowerProfileUser?.Department?.DepartmentName,
                Program = user.BorrowerProfileUser?.Program,
                YearLevel = user.BorrowerProfileUser?.YearLevel,
                EligibilityStatus = user.BorrowerProfileUser?.EligibilityStatus,
                SuspensionReason = user.BorrowerProfileUser?.SuspensionReason,
                SuspendedUntil = user.BorrowerProfileUser?.SuspendedUntil
            };

            if (!string.IsNullOrWhiteSpace(user.ProfilePhotoStoragePath))
            {
                model.ProfilePhotoUrl = await _profilePhotoService.CreateReadUrlAsync(
                    user.ProfilePhotoStoragePath,
                    cancellationToken);
            }

            return model;
        }
    }
}
