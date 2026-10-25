using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Exceptions;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using ErrorMessages = ASI.Basecode.Resources.Messages.Errors;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// WBS 5.1 User and Role Management.
    /// Passwords use the team's PasswordManager so AccountService (login) can verify them.
    /// Server-side rules:
    ///  - User ID (user_code) and email are unique (UQ_Users_user_code, UQ_Accounts_email).
    ///  - The role must exist and be active to be assigned.
    ///  - Administrators cannot change their own role or deactivate their own account.
    ///  - The last active Admin account cannot be demoted or deactivated.
    ///  - Edits and status changes are rejected if the account changed after the form was loaded.
    ///  - Each change is written to AuditLogs in the same transaction.
    /// </summary>
    public class UserAccountService : IUserAccountService
    {
        private readonly IUserRepository _userRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UserAccountService(
            IUserRepository userRepository,
            IAccountRepository accountRepository,
            IRoleRepository roleRepository,
            IAuditLogRepository auditLogRepository,
            IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _accountRepository = accountRepository;
            _roleRepository = roleRepository;
            _auditLogRepository = auditLogRepository;
            _unitOfWork = unitOfWork;
        }

        #region View

        public List<UserAccountListItem> GetUserAccounts()
        {
            return Project(_accountRepository.GetAccounts())
                .OrderBy(account => account.LastName)
                .ThenBy(account => account.FirstName)
                .ToList();
        }

        public UserAccountListItem GetUserAccountById(int userId)
        {
            return Project(_accountRepository.GetAccounts().Where(account => account.UserId == userId))
                .SingleOrDefault();
        }

        public List<RoleListItem> GetRoles()
        {
            return _roleRepository.GetRoles()
                .OrderBy(role => role.RoleId)
                .Select(role => new RoleListItem
                {
                    RoleId = role.RoleId,
                    RoleName = role.RoleName,
                    Description = role.Description ?? string.Empty,
                    IsActive = role.IsActive,
                    TotalAccountCount = role.Accounts.Count,
                    ActiveAccountCount = role.Accounts.Count(account => account.AccountStatus == AccountStatuses.Active)
                })
                .ToList();
        }

        private static IQueryable<UserAccountListItem> Project(IQueryable<Account> accounts)
        {
            return accounts.Select(account => new UserAccountListItem
            {
                UserId = account.UserId,
                AccountId = account.AccountId,
                UserCode = account.User.UserCode,
                FirstName = account.User.FirstName,
                MiddleName = account.User.MiddleName ?? string.Empty,
                LastName = account.User.LastName,
                Suffix = account.User.Suffix ?? string.Empty,
                ContactNumber = account.User.ContactNumber ?? string.Empty,
                Email = account.Email,
                RoleId = account.RoleId,
                RoleName = account.Role.RoleName,
                AccountStatus = account.AccountStatus,
                FailedLoginAttempts = account.FailedLoginAttempts,
                LockoutEndAt = account.LockoutEndAt,
                LastLoginAt = account.LastLoginAt,
                CreatedBy = account.CreatedBy,
                CreatedAt = account.CreatedAt,
                UpdatedBy = account.UpdatedBy ?? string.Empty,
                UpdatedAt = account.UpdatedAt
            });
        }

        #endregion

        #region Create / Edit (assign and change role)

        public int AddUserAccount(UserAccountFormViewModel model, int currentUserId, string createdBy, string ipAddress)
        {
            Normalize(model);

            var errors = GetDuplicateErrors(model.UserCode, model.Email);
            if (string.IsNullOrEmpty(model.Password))
                errors[nameof(UserAccountFormViewModel.Password)] = "Password is required.";
            var role = GetAssignableRole(model.RoleId, null, errors);
            if (errors.Count > 0)
                throw new UserAccountValidationException(errors);

            var now = DateTime.Now;
            var user = new User
            {
                UserCode = model.UserCode,
                FirstName = model.FirstName,
                MiddleName = model.MiddleName,
                LastName = model.LastName,
                Suffix = model.Suffix,
                ContactNumber = model.ContactNumber,
                CreatedBy = createdBy,
                CreatedAt = now,
                Account = new Account
                {
                    RoleId = role.RoleId,
                    Email = model.Email,
                    PasswordHash = PasswordManager.EncryptPassword(model.Password),
                    AccountStatus = model.AccountStatus,
                    FailedLoginAttempts = 0,
                    CreatedBy = createdBy,
                    CreatedAt = now
                }
            };

            SaveInTransaction(() =>
            {
                _userRepository.AddUser(user); // inserts Users + Accounts together
                AddAudit(currentUserId, createdBy, ipAddress, UserAccountAuditActions.Created, user.UserId,
                    $"Created {user.UserCode} ({user.Account.Email}) with role {role.RoleName} and status {user.Account.AccountStatus}.");
            });

            return user.UserId;
        }

        public bool EditUserAccount(UserAccountFormViewModel model, int currentUserId, string updatedBy, string ipAddress)
        {
            Normalize(model);

            var account = _accountRepository.GetAccountByUserId(model.UserId);
            if (account == null)
                return false;

            EnsureNotChanged(account, model.Version);

            var errors = GetDuplicateErrors(model.UserCode, model.Email, model.UserId);
            var role = GetAssignableRole(model.RoleId, account.RoleId, errors);
            if (errors.Count > 0)
                throw new UserAccountValidationException(errors);

            var roleChanged = role.RoleId != account.RoleId;
            var statusChanged = model.AccountStatus != account.AccountStatus;

            if (model.UserId == currentUserId)
            {
                if (roleChanged)
                    throw new UserAccountValidationException(nameof(UserAccountFormViewModel.RoleId), ErrorMessages.CannotChangeOwnRole);
                if (statusChanged && model.AccountStatus != AccountStatuses.Active)
                    throw new UserAccountValidationException(nameof(UserAccountFormViewModel.AccountStatus), ErrorMessages.CannotDeactivateOwnAccount);
            }

            if (IsActiveAdmin(account) && (roleChanged || model.AccountStatus != AccountStatuses.Active))
                EnsureAnotherActiveAdmin(account);

            var changes = new List<string>();
            Track(changes, "User ID", account.User.UserCode, model.UserCode);
            Track(changes, "First name", account.User.FirstName, model.FirstName);
            Track(changes, "Middle name", account.User.MiddleName, model.MiddleName);
            Track(changes, "Last name", account.User.LastName, model.LastName);
            Track(changes, "Suffix", account.User.Suffix, model.Suffix);
            Track(changes, "Contact number", account.User.ContactNumber, model.ContactNumber);
            Track(changes, "Email", account.Email, model.Email);
            Track(changes, "Role", account.Role.RoleName, role.RoleName);
            Track(changes, "Status", account.AccountStatus, model.AccountStatus);

            if (changes.Count == 0)
                return true;

            var now = DateTime.Now;
            account.User.UserCode = model.UserCode;
            account.User.FirstName = model.FirstName;
            account.User.MiddleName = model.MiddleName;
            account.User.LastName = model.LastName;
            account.User.Suffix = model.Suffix;
            account.User.ContactNumber = model.ContactNumber;
            account.User.UpdatedBy = updatedBy;
            account.User.UpdatedAt = now;

            account.Email = model.Email;
            account.RoleId = role.RoleId;
            account.AccountStatus = model.AccountStatus;
            account.UpdatedBy = updatedBy;
            account.UpdatedAt = now;

            SaveInTransaction(() =>
            {
                _accountRepository.UpdateAccount(account); // also saves the tracked User changes
                AddAudit(currentUserId, updatedBy, ipAddress,
                    roleChanged ? UserAccountAuditActions.RoleChanged : UserAccountAuditActions.Updated,
                    account.UserId, $"Updated {model.UserCode}: {string.Join("; ", changes)}");
            });

            return true;
        }

        #endregion

        #region Activate / Deactivate / Reset access

        public bool SetUserAccountStatus(int userId, bool activate, string version, int currentUserId, string updatedBy, string ipAddress)
        {
            var account = _accountRepository.GetAccountByUserId(userId);
            if (account == null)
                return false;

            EnsureNotChanged(account, version);

            var newStatus = activate ? AccountStatuses.Active : AccountStatuses.Inactive;
            if (account.AccountStatus == newStatus)
                return true;

            if (!activate)
            {
                if (userId == currentUserId)
                    throw new UserAccountValidationException(string.Empty, ErrorMessages.CannotDeactivateOwnAccount);
                if (IsActiveAdmin(account))
                    EnsureAnotherActiveAdmin(account);
            }
            else if (!account.Role.IsActive)
            {
                throw new UserAccountValidationException(string.Empty, ErrorMessages.RoleInactive);
            }

            var previousStatus = account.AccountStatus;
            account.AccountStatus = newStatus;
            if (activate)
            {
                account.FailedLoginAttempts = 0;
                account.LockoutEndAt = null;
            }
            account.UpdatedBy = updatedBy;
            account.UpdatedAt = DateTime.Now;

            SaveInTransaction(() =>
            {
                _accountRepository.UpdateAccount(account);
                AddAudit(currentUserId, updatedBy, ipAddress,
                    activate ? UserAccountAuditActions.Activated : UserAccountAuditActions.Deactivated,
                    userId, $"Status of {account.User.UserCode}: {previousStatus} -> {newStatus}.");
            });

            return true;
        }

        /// <summary>
        /// Reset access = new one-time temporary password + clear failed attempts and lockout.
        /// Returns the temporary password (shown once to the admin, never logged), or null if the user does not exist.
        /// </summary>
        public string ResetUserAccountAccess(int userId, int currentUserId, string updatedBy, string ipAddress)
        {
            var account = _accountRepository.GetAccountByUserId(userId);
            if (account == null)
                return null;

            var temporaryPassword = GenerateTemporaryPassword();
            account.PasswordHash = PasswordManager.EncryptPassword(temporaryPassword);
            account.FailedLoginAttempts = 0;
            account.LockoutEndAt = null;
            account.UpdatedBy = updatedBy;
            account.UpdatedAt = DateTime.Now;

            SaveInTransaction(() =>
            {
                _accountRepository.UpdateAccount(account);
                AddAudit(currentUserId, updatedBy, ipAddress, UserAccountAuditActions.AccessReset, userId,
                    $"Temporary password issued and lockout cleared for {account.User.UserCode}.");
            });

            return temporaryPassword;
        }

        #endregion

        #region Helpers

        private Dictionary<string, string> GetDuplicateErrors(string userCode, string email, int? excludeUserId = null)
        {
            var errors = new Dictionary<string, string>();

            if (_userRepository.UserCodeExists(userCode, excludeUserId))
                errors.Add(nameof(UserAccountFormViewModel.UserCode), ErrorMessages.UserCodeExists);

            if (_accountRepository.EmailExists(email, excludeUserId))
                errors.Add(nameof(UserAccountFormViewModel.Email), ErrorMessages.EmailExists);

            return errors;
        }

        /// <summary>
        /// Returns the role to assign. An inactive role may be kept by a user who already has it, but not newly assigned.
        /// </summary>
        private Role GetAssignableRole(byte? roleId, byte? currentRoleId, Dictionary<string, string> errors)
        {
            var role = roleId.HasValue ? _roleRepository.GetRoleById(roleId.Value) : null;

            if (role == null)
                errors[nameof(UserAccountFormViewModel.RoleId)] = ErrorMessages.RoleNotFound;
            else if (!role.IsActive && role.RoleId != currentRoleId)
                errors[nameof(UserAccountFormViewModel.RoleId)] = ErrorMessages.RoleInactive;

            return role;
        }

        private static void EnsureNotChanged(Account account, string version)
        {
            var current = account.UpdatedAt.HasValue ? account.UpdatedAt.Value.Ticks.ToString() : "0";
            if (!string.Equals(current, string.IsNullOrEmpty(version) ? "0" : version, StringComparison.Ordinal))
                throw new UserAccountValidationException(string.Empty, ErrorMessages.UserAccountChanged);
        }

        private static bool IsActiveAdmin(Account account)
        {
            return account.AccountStatus == AccountStatuses.Active
                && string.Equals(account.Role?.RoleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureAnotherActiveAdmin(Account account)
        {
            if (_accountRepository.CountActiveAccountsByRole(account.RoleId, account.UserId) == 0)
                throw new UserAccountValidationException(string.Empty, ErrorMessages.LastActiveAdmin);
        }

        private void AddAudit(int currentUserId, string createdBy, string ipAddress, string action, int targetUserId, string details)
        {
            _auditLogRepository.AddAuditLog(new AuditLog
            {
                UserId = currentUserId > 0 ? currentUserId : null,
                Action = action,
                EntityName = "Accounts",
                EntityId = targetUserId.ToString(CultureInfo.InvariantCulture),
                Details = details,
                IpAddress = ipAddress != null && ipAddress.Length > 45 ? ipAddress.Substring(0, 45) : ipAddress,
                CreatedBy = createdBy ?? Const.System,
                CreatedAt = DateTime.Now
            });
        }

        /// <summary>
        /// Runs repository saves in one transaction and turns unique-key violations
        /// (e.g. a double-submitted form) into field errors.
        /// </summary>
        private void SaveInTransaction(Action work)
        {
            using var transaction = _unitOfWork.Database.Database.BeginTransaction();
            try
            {
                work();
                transaction.Commit();
            }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && (sql.Number == 2627 || sql.Number == 2601))
            {
                transaction.Rollback();
                if (sql.Message.Contains("UQ_Users_user_code", StringComparison.OrdinalIgnoreCase))
                    throw new UserAccountValidationException(nameof(UserAccountFormViewModel.UserCode), ErrorMessages.UserCodeExists);
                if (sql.Message.Contains("UQ_Accounts_email", StringComparison.OrdinalIgnoreCase))
                    throw new UserAccountValidationException(nameof(UserAccountFormViewModel.Email), ErrorMessages.EmailExists);
                throw;
            }
        }

        private static void Track(List<string> changes, string label, string oldValue, string newValue)
        {
            if (!string.Equals(oldValue ?? string.Empty, newValue ?? string.Empty, StringComparison.Ordinal))
                changes.Add($"{label}: \"{oldValue}\" -> \"{newValue}\"");
        }

        private static void Normalize(UserAccountFormViewModel model)
        {
            static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

            model.UserCode = Clean(model.UserCode)?.ToUpperInvariant();
            model.FirstName = Clean(model.FirstName);
            model.MiddleName = Clean(model.MiddleName);
            model.LastName = Clean(model.LastName);
            model.Suffix = Clean(model.Suffix);
            model.ContactNumber = Clean(model.ContactNumber);
            model.Email = Clean(model.Email)?.ToLowerInvariant();
            model.AccountStatus = Clean(model.AccountStatus) ?? AccountStatuses.Active;
        }

        /// <summary>12 characters with upper, lower, digit and symbol; ambiguous characters excluded.</summary>
        private static string GenerateTemporaryPassword()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnopqrstuvwxyz";
            const string digits = "23456789";
            const string symbols = "@#$%&*?";
            var all = upper + lower + digits + symbols;

            var chars = new List<char>
            {
                upper[RandomNumberGenerator.GetInt32(upper.Length)],
                lower[RandomNumberGenerator.GetInt32(lower.Length)],
                digits[RandomNumberGenerator.GetInt32(digits.Length)],
                symbols[RandomNumberGenerator.GetInt32(symbols.Length)]
            };
            while (chars.Count < 12)
                chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);

            for (var i = chars.Count - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }
            return new string(chars.ToArray());
        }

        #endregion
    }
}
