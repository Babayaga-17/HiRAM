// ASI.Basecode.Services/Services/BorrowerProfileService.cs
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.ServiceModels.Borrower;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{

    public class BorrowerProfileService : IBorrowerProfileService
    {
        private readonly IBorrowerProfileRepository _borrowerProfileRepository;
        private readonly INotificationService _notificationService;
        private readonly IUserRepository _userRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IProfilePhotoService _profilePhotoService;
        private readonly IEmailManager _emailManager;
        private readonly ILogger<BorrowerProfileService> _logger;

        public BorrowerProfileService(IBorrowerProfileRepository borrowerProfileRepository, 
            IUserRepository userRepository,
            IAccountRepository accountRepository, 
            IProfilePhotoService profilePhotoService,
            IEmailManager emailManager, 
            ILogger<BorrowerProfileService> logger, 
            INotificationService notificationService)
        {
            _borrowerProfileRepository = borrowerProfileRepository;
            _notificationService = notificationService;
            _userRepository = userRepository;
            _accountRepository = accountRepository;
            _profilePhotoService = profilePhotoService;
            _emailManager = emailManager;
            _logger = logger;
        }

        public List<Department> GetDepartments() =>
            _borrowerProfileRepository.GetActiveDepartments();

        public BorrowerPageViewModel GetPage(string search, string status, int page, int pageSize)
        {
            if (pageSize < 1) pageSize = 5;
            var filterStatus = status == EligibilityStatuses.Eligible || status == EligibilityStatuses.Suspended ? status : null;
            var filterSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

            var vm = new BorrowerPageViewModel
            {
                Search = search,
                Status = status,
                PageSize = pageSize,
                Departments = GetDepartments(),
                OverallTotal = _borrowerProfileRepository.CountProfiles(),
                OverallEligible = _borrowerProfileRepository.CountProfiles(EligibilityStatuses.Eligible),
                OverallSuspended = _borrowerProfileRepository.CountProfiles(EligibilityStatuses.Suspended)
            };

            vm.TotalCount = _borrowerProfileRepository.CountMatchingProfiles(filterSearch, filterStatus);
            vm.TotalPages = Math.Max(1, (int)Math.Ceiling(vm.TotalCount / (double)pageSize));
            vm.Page = Math.Min(Math.Max(page, 1), vm.TotalPages);

            vm.Rows = _borrowerProfileRepository.GetPage(filterSearch, filterStatus, (vm.Page - 1) * pageSize, pageSize)
                       .Select(b => new BorrowerRowViewModel
                       {
                           UserId = b.UserId,
                           UserCode = b.User.UserCode,
                           FullName = string.Join(" ", new[] { b.User.FirstName, b.User.MiddleName, b.User.LastName, b.User.Suffix }
                                                       .Where(x => !string.IsNullOrWhiteSpace(x))),
                           ContactNumber = b.User.ContactNumber,
                           DepartmentId = b.DepartmentId,
                           BorrowerType = b.BorrowerType,
                           DepartmentName = b.Department?.DepartmentName,
                           Program = b.Program,
                           YearLevel = b.YearLevel,
                           EligibilityStatus = b.EligibilityStatus,
                           SuspensionReason = b.SuspensionReason,
                           SuspendedUntil = b.SuspendedUntil,
                           CreatedAt = b.CreatedAt
                       }).ToList();
            return vm;
        }

        public BorrowerHistoryViewModel GetHistory(int userId)
        {
            var b = _borrowerProfileRepository.GetForHistory(userId);
            if (b == null) return null;

            var reservations = _borrowerProfileRepository.GetReservations(userId);

            return new BorrowerHistoryViewModel
            {
                UserId = userId,
                UserCode = b.User.UserCode,
                FullName = string.Join(" ", new[] { b.User.FirstName, b.User.MiddleName, b.User.LastName, b.User.Suffix }
                                            .Where(x => !string.IsNullOrWhiteSpace(x))),
                Rows = reservations.Select(r => new HistoryRowViewModel
                {
                    ReservationNumber = r.ReservationNumber,
                    Purpose = r.Purpose,
                    Status = r.Status,
                    RequestedAt = r.RequestedAt,
                    StartDateTime = r.StartDateTime,
                    ExpectedReturnDateTime = r.ExpectedReturnDateTime,
                    BorrowedAt = r.BorrowedAt,
                    ReturnedAt = r.ReturnedAt,
                    Items = r.ReservationItems.Select(i => new HistoryItemViewModel
                    {
                        EquipmentName = i.Equipment?.EquipmentName,
                        EquipmentCode = i.Equipment?.EquipmentCode,
                        IsReturned = i.IsReturned
                    }).ToList()
                }).ToList()
            };
        }

        // Edit: only department, program, year level and eligibility status can change.
        // The user's name, contact number and user code are never touched here.
        public (bool ok, string message) Save(BorrowerFormModel m, string actor)
        {
            if (m.UserId == 0) return (false, "Select an existing borrower to edit.");
            var department = GetDepartments().FirstOrDefault(d => d.DepartmentId == m.DepartmentId);
            if (department == null) return (false, "Select an active department.");

            var profile = _borrowerProfileRepository.GetForUpdate(m.UserId);
            if (profile == null) return (false, "Borrower not found.");

            var program = m.Program?.Trim();
            if (profile.BorrowerType != "Faculty"
                && !ProgramOptions.IsProgramAllowed(department.DepartmentCode, program))
                return (false, "Select a program belonging to the department.");


            var now = DateTime.Now;
            profile.DepartmentId = department.DepartmentId;
            profile.Program = profile.BorrowerType == "Faculty" ? null : program;
            profile.YearLevel = profile.BorrowerType == "Faculty" ? null : m.YearLevel;
            profile.EligibilityStatus = m.EligibilityStatus == EligibilityStatuses.Suspended ? EligibilityStatuses.Suspended : EligibilityStatuses.Eligible;

            if (profile.EligibilityStatus == EligibilityStatuses.Suspended)
            {
                profile.SuspensionReason = m.SuspensionReason?.Trim();
                profile.SuspendedUntil = m.SuspendedUntil;
                profile.SuspendedAt ??= now;
                // profile.SuspendedByUserId = <current admin's UserId>;  // set this if you can get it from the logged-in account
            }
            else
            {
                profile.SuspensionReason = null;
                profile.SuspendedUntil = null;
                profile.SuspendedAt = null;
                profile.SuspendedByUserId = null;
            }

            profile.UpdatedBy = actor; profile.UpdatedAt = now;
            _borrowerProfileRepository.SaveChanges();
            return (true, "Borrower updated.");
        }

        public (bool ok, string message) SetStatus(int userId, bool suspend, string reason, DateTime? until, string actor)
        {
            var e = _borrowerProfileRepository.GetForUpdate(userId);
            if (e == null) return (false, "Borrower not found.");
            var now = DateTime.Now;
            if (suspend)
            {
                e.EligibilityStatus = EligibilityStatuses.Suspended;
                e.SuspensionReason = reason?.Trim();
                e.SuspendedUntil = until;
                e.SuspendedAt = now;
            }
            else
            {
                e.EligibilityStatus = EligibilityStatuses.Eligible;
                e.SuspensionReason = null;
                e.SuspendedUntil = null;
                e.SuspendedAt = null;
                e.SuspendedByUserId = null;
            }
            e.UpdatedBy = actor; e.UpdatedAt = now;
            _borrowerProfileRepository.SaveChanges();
            return (true, suspend ? "Borrower suspended." : "Borrower activated.");
        }

        public async Task<bool> RegisterAsync(BorrowerRegistrationViewModel model, IFormFile photo, CancellationToken cancellationToken = default)
        {
            model.UserCode = model.UserCode?.Trim(); model.Email = model.Email?.Trim();

            Validator.ValidateObject(model, new ValidationContext(model), true);
                
            if (_userRepository.UserCodeExists(model.UserCode))
                throw new ValidationException("That student or employee ID is already registered.");    

            if (_accountRepository.EmailExists(model.Email))
                throw new ValidationException("That email is already registered.");

            var department = GetDepartments().FirstOrDefault(d => d.DepartmentId == model.DepartmentId);
            if (department == null) 
                throw new ValidationException("Select an active department.");
            model.Program = model.Program?.Trim();
            if (model.BorrowerType == "Student" && !ProgramOptions.IsProgramAllowed(department.DepartmentCode, model.Program))
                throw new ValidationException("Select a program belonging to the department.");
            var now = DateTime.Now;
            var user = new User
            {
                UserCode = model.UserCode,
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                MiddleName = model.MiddleName?.Trim(),
                Suffix = model.Suffix?.Trim(),
                ContactNumber = model.ContactNumber?.Trim(),
                CreatedBy = "Registration",
                CreatedAt = now,
                Account = new Account
                {
                    RoleName = RoleNames.Borrower,
                    Email = model.Email,
                    PasswordHash = PasswordManager.EncryptPassword(model.Password),
                    AccountStatus = AccountStatuses.PendingApproval,
                    CreatedBy = "Registration",
                    CreatedAt = now
                },
                BorrowerProfileUser = new BorrowerProfile
                {
                    DepartmentId = department.DepartmentId,
                    BorrowerType = model.BorrowerType,
                    Program = model.BorrowerType == "Student" ? model.Program.Trim() : null,
                    YearLevel = model.BorrowerType == "Student" ? model.YearLevel : null,
                    EligibilityStatus = EligibilityStatuses.Ineligible,
                    CreatedBy = "Registration",
                    CreatedAt = now
                }
            };
            string path = null;
            try
            {
                if (photo != null)
                    path = await _profilePhotoService.UploadProfileAsync(photo, cancellationToken);

                user.ProfilePhotoStoragePath = path;

                // One SaveChanges inserts this complete related graph in an EF transaction.
                _notificationService.RegistrationSubmitted(user);
                _userRepository.AddUser(user);
            }
            catch (Exception exception)
            {
                if (path != null)
                {
                    try { await _profilePhotoService.DeleteAsync(path, CancellationToken.None); }
                    catch (Exception cleanup) { _logger.LogWarning(cleanup, "Unable to clean up registration photo after a failed save."); }
                }
                if (exception is DbUpdateException && exception.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
                    throw new ValidationException("That email or school ID was just registered. Please sign in or contact your administrator.");
                throw;
            }

            try
            {
                await _emailManager.SendAsync(model.Email, 
                    "HiRAM registration awaiting approval",
                    "Your registration has been received and is awaiting administrator approval. You cannot sign in until it is approved. Please do not register again while your request is pending.");
                return true;
            }
            catch (Exception exception) { 
                _logger.LogWarning(exception, "Registration saved but receipt email failed for user {UserId}.", user.UserId); return false; 
            }
        }
    }
}
