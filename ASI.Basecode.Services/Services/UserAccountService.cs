using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Exceptions;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ServiceModels.UserAccount;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
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
    /// </summary>
    public class UserAccountService : IUserAccountService
    {
        private const string SuspendedEligibility = "Suspended";
        private readonly IUserRepository _userRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IBorrowerProfileRepository _borrowerProfileRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notifications;

        public UserAccountService(
            IUserRepository userRepository,
            IAccountRepository accountRepository,
            IBorrowerProfileRepository borrowerProfileRepository,
            IUnitOfWork unitOfWork,
            INotificationService notifications)
        {
            _userRepository = userRepository;
            _accountRepository = accountRepository;
            _borrowerProfileRepository = borrowerProfileRepository;
            _unitOfWork = unitOfWork;
            _notifications = notifications;
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
            var counts = _accountRepository.GetAccountCountsByRole();

            return RoleNames.All.Select(roleName =>
            {
                counts.TryGetValue(roleName, out var count);

                return new RoleListItem
                {
                    RoleName = roleName,
                    TotalAccountCount = count.Total,
                    ActiveAccountCount = count.Active
                };
            }).ToList();
        }

        /// <summary>Active database departments used by account forms.</summary>
        public List<Department> GetActiveDepartments() => _borrowerProfileRepository.GetActiveDepartments();

        /// <summary>Copies the stored BorrowerProfile into the form model (edit screen only).</summary>
        public void FillBorrowerProfile(UserAccountFormViewModel model)
        {
            if (model == null || model.UserId <= 0)
                return;

            var profile = _borrowerProfileRepository.GetForUpdate(model.UserId);
            if (profile == null)
                return;

            model.DepartmentId = profile.DepartmentId;
            model.Program = profile.Program ?? string.Empty;
            model.YearLevel = profile.YearLevel;
            model.BorrowerType = profile.BorrowerType ?? string.Empty;
            model.EligibilityStatus = profile.EligibilityStatus ?? EligibilityStatuses.Eligible;
            model.EligibilityLocked = IsEligibilityLocked(model.AccountStatus, profile);
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
                RoleName = account.RoleName,
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
            var role = GetAssignableRole(model.RoleName, errors);
            var isBorrower = IsBorrowerRole(role);
            var department = isBorrower ? ValidateBorrowerProfile(model, errors) : null;
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
                    RoleName = role,
                    Email = model.Email,
                    PasswordHash = PasswordManager.EncryptPassword(model.Password),
                    AccountStatus = model.AccountStatus,
                    FailedLoginAttempts = 0,
                    CreatedBy = createdBy,
                    CreatedAt = now
                }
            };

            if (isBorrower)
            {
                user.BorrowerProfileUser = new BorrowerProfile
                {
                    DepartmentId = department.DepartmentId,
                    Program = model.BorrowerType == "Faculty" ? null : model.Program,
                    YearLevel = model.BorrowerType == "Faculty" ? null : model.YearLevel,
                    BorrowerType = model.BorrowerType,
                    EligibilityStatus = model.EligibilityStatus,
                    CreatedBy = createdBy,
                    CreatedAt = now
                };
            }

            SaveInTransaction(() =>
            {
                _userRepository.AddUser(user); // inserts Users + Accounts together
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

            if ((account.AccountStatus == AccountStatuses.PendingApproval || model.AccountStatus == AccountStatuses.PendingApproval)
                && (model.AccountStatus != account.AccountStatus || model.RoleName != account.RoleName))
                throw new UserAccountValidationException(string.Empty, "Use registration approval to activate a pending borrower. Its role cannot be changed while pending.");

            var errors = GetDuplicateErrors(model.UserCode, model.Email, model.UserId);
            var role = GetAssignableRole(model.RoleName, errors);
            var profile = _borrowerProfileRepository.GetForUpdate(model.UserId);
            var isBorrower = IsBorrowerRole(role);

            model.EligibilityLocked = profile != null && IsEligibilityLocked(account.AccountStatus, profile);
            Department department = null;
            if (isBorrower)
            {
                // A locked eligibility keeps the stored value, whatever the form posted.
                if (model.EligibilityLocked)
                    model.EligibilityStatus = profile.EligibilityStatus;
                department = ValidateBorrowerProfile(model, errors);
            }

            if (errors.Count > 0)
                throw new UserAccountValidationException(errors);

            var roleChanged = role != account.RoleName;
            var statusChanged = model.AccountStatus != account.AccountStatus;

            if (model.UserId == currentUserId)
            {
                if (roleChanged)
                    throw new UserAccountValidationException(nameof(UserAccountFormViewModel.RoleName), ErrorMessages.CannotChangeOwnRole);
                if (statusChanged && model.AccountStatus != AccountStatuses.Active)
                    throw new UserAccountValidationException(nameof(UserAccountFormViewModel.AccountStatus), ErrorMessages.CannotDeactivateOwnAccount);
            }

            if (!isBorrower && profile != null && _borrowerProfileRepository.HasReservations(model.UserId))
                throw new UserAccountValidationException(nameof(UserAccountFormViewModel.RoleName),
                    "This borrower has reservations, so the Borrower details cannot be removed. Resolve or cancel the reservations first.");

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
            Track(changes, "Role", account.RoleName, role);
            Track(changes, "Status", account.AccountStatus, model.AccountStatus);   

            if (isBorrower)
            {
                Track(changes, "Department", Text(profile?.DepartmentId), Text(model.DepartmentId));
                Track(changes, "Program", profile?.Program, model.Program);
                Track(changes, "Year level", Text(profile?.YearLevel), Text(model.YearLevel));
                Track(changes, "Borrower type", profile?.BorrowerType, model.BorrowerType);
                Track(changes, "Eligibility", profile?.EligibilityStatus, model.EligibilityStatus);
            }
            else if (profile != null)
            {
                changes.Add("Borrower details removed");
            }

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
            account.RoleName = role;
            account.AccountStatus = model.AccountStatus;
            account.UpdatedBy = updatedBy;
            account.UpdatedAt = now;

            if (isBorrower)
            {
                if (profile == null)
                {
                    profile = new BorrowerProfile
                    {
                        UserId = model.UserId,
                        CreatedBy = updatedBy,
                        CreatedAt = now
                    };
                    account.User.BorrowerProfileUser = profile; // EF inserts it with the tracked User
                }
                ApplyBorrowerProfile(profile, model, department, updatedBy, now);
            }
            else if (profile != null)
            {
               _borrowerProfileRepository.Delete(profile);
            }

            SaveInTransaction(() =>
            {
                _accountRepository.UpdateAccount(account); // also saves the tracked User changes
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

            if (account.AccountStatus == AccountStatuses.PendingApproval)
                throw new UserAccountValidationException(string.Empty, "Review and approve this registration instead of changing its account status.");

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
            else if (account.RoleName != RoleNames.Borrower 
                && account.RoleName != RoleNames.Custodian 
                && account.RoleName != RoleNames.Admin)
            {
                throw new UserAccountValidationException(string.Empty, "The account has an invalid role.");
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
            });

            return true;
        }

        /// <summary>
        /// Approves the pending account and borrower eligibility in a repository-owned transaction.
        /// </summary>
        public Task<bool> ApproveRegistrationAsync(int userId, string version, string updatedBy)
        {   
            var found = _accountRepository.UpdateRegistration(userId, account =>
            {
                EnsureNotChanged(account, version);
                if (account.AccountStatus != AccountStatuses.PendingApproval)
                    throw new UserAccountValidationException(string.Empty, "Only pending registrations can be approved. Refresh the account details.");
                if (account.RoleName != RoleNames.Borrower)
                    throw new UserAccountValidationException(string.Empty, "Registration approval requires a Borrower account.");
                var profile = account.User?.BorrowerProfileUser;
                if (profile == null || profile.EligibilityStatus != EligibilityStatuses.Ineligible
                    || profile.SuspendedAt.HasValue || profile.SuspendedUntil.HasValue
                    || profile.SuspendedByUserId.HasValue || !string.IsNullOrWhiteSpace(profile.SuspensionReason))
                    throw new UserAccountValidationException(string.Empty, "The borrower profile is missing or requires separate eligibility review. Approval was not saved.");

                var now = DateTime.Now;
                account.AccountStatus = AccountStatuses.Active;
                account.FailedLoginAttempts = 0;
                account.LockoutEndAt = null;
                account.UpdatedBy = updatedBy;
                account.UpdatedAt = now;
                profile.EligibilityStatus = EligibilityStatuses.Eligible;
                profile.UpdatedBy = updatedBy;
                profile.UpdatedAt = now;
                _notifications.RegistrationApproved(account);
            });
            return Task.FromResult(found);
        }

        /// <summary>Issues a temporary password and clears failed attempts and lockout.</summary>
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
            });

            return temporaryPassword;
        }

        #endregion

        #region Borrower details

        /// <summary>
        /// Borrower fields are required when the role is Borrower. Eligibility is skipped when it is locked,
        /// because the stored value is kept in that case.
        /// </summary>
        private Department ValidateBorrowerProfile(UserAccountFormViewModel model, Dictionary<string, string> errors)
        {
            var department = _borrowerProfileRepository.GetActiveDepartments()
                .FirstOrDefault(item => item.DepartmentId == model.DepartmentId);
            if (department == null)
                errors[nameof(UserAccountFormViewModel.DepartmentId)] = "Select an active department.";

            if (model.BorrowerType != "Faculty")
            {
                if (!ProgramOptions.IsProgramAllowed(department?.DepartmentCode, model.Program))
                    errors[nameof(UserAccountFormViewModel.Program)] = "Select a program belonging to the department.";
                if (!model.YearLevel.HasValue || model.YearLevel.Value < 1 || model.YearLevel.Value > 5)
                    errors[nameof(UserAccountFormViewModel.YearLevel)] = "Select a year level from 1 to 5.";
            }
            else
            {
                model.Program = null;
                model.YearLevel = null;
            }

            if (!UserAccountFormViewModel.BorrowerTypeOptions.Contains(model.BorrowerType))
                errors[nameof(UserAccountFormViewModel.BorrowerType)] = "Select Student or Faculty.";

            if (!model.EligibilityLocked && !UserAccountFormViewModel.EligibilityOptions.Contains(model.EligibilityStatus))
                errors[nameof(UserAccountFormViewModel.EligibilityStatus)] = "Select a valid eligibility status.";

            return department;
        }

        private static void ApplyBorrowerProfile(BorrowerProfile profile, UserAccountFormViewModel model, Department department, string updatedBy, DateTime now)
        {
            profile.DepartmentId = department.DepartmentId;
            profile.Program = model.Program;
            profile.YearLevel = model.YearLevel;
            profile.BorrowerType = model.BorrowerType;
            profile.EligibilityStatus = model.EligibilityStatus;
            profile.UpdatedBy = updatedBy;
            profile.UpdatedAt = now;
        }

        /// <summary>Pending registrations and suspended borrowers keep their stored eligibility.</summary>
        private static bool IsEligibilityLocked(string accountStatus, BorrowerProfile profile)
        {
            return accountStatus == AccountStatuses.PendingApproval
                || profile.EligibilityStatus == SuspendedEligibility
                || profile.SuspendedAt.HasValue;
        }

        private static bool IsBorrowerRole(string roleName)
        {
            return roleName == RoleNames.Borrower;
        }

        private static string Text(object value)
        {
            return value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture);
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
        /// Validates the fixed account role names.
        /// </summary>
        private static string GetAssignableRole(string roleName, Dictionary<string, string> errors)
        {
            if (!RoleNames.All.Contains(roleName))
            {
                errors[nameof(UserAccountFormViewModel.RoleName)] = "Select a valid role.";

                return null;
            }

            return roleName;
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
                && string.Equals(
                    account.RoleName,
                    RoleNames.Admin,
                    StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureAnotherActiveAdmin(Account account)
        {
            if (_accountRepository.CountActiveAccountsByRole(RoleNames.Admin, account.UserId) == 0)
            {
                throw new UserAccountValidationException(string.Empty, ErrorMessages.LastActiveAdmin);
            }
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
            model.Program = Clean(model.Program);
            model.BorrowerType = Clean(model.BorrowerType);
            model.EligibilityStatus = Clean(model.EligibilityStatus) ?? EligibilityStatuses.Eligible;
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

        #region Admin Dashboard Methods

        public Task<Dictionary<string, int>> CountAccountsByStatusAsync()
        {
            return _accountRepository.CountAccountsByStatusAsync();
        }

        public async Task<List<UserAccountListItem>> GetPendingRegistrationPreviewAsync()
        {
            var accounts = await _accountRepository.GetPendingRegistrationPreviewAsync();

            return accounts.Select(account => new UserAccountListItem
            {
                UserId = account.UserId,
                AccountId = account.AccountId,
                UserCode = account.User.UserCode,
                FirstName = account.User.FirstName,
                MiddleName = account.User.MiddleName ?? string.Empty,
                LastName = account.User.LastName,
                Suffix = account.User.Suffix ?? string.Empty,
                AccountStatus = account.AccountStatus
            }).ToList();
        }

        #endregion
    }
}
