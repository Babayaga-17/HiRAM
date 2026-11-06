// ASI.Basecode.Services/Services/BorrowerProfileService.cs
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ServiceModels;
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
        private readonly IBorrowerProfileRepository _repository;
        private readonly IUserRepository users;
        private readonly IAccountRepository accounts;
        private readonly IRoleRepository roles;
        private readonly IProfilePhotoService photos;
        private readonly IEmailManager email;
        private readonly ILogger<BorrowerProfileService> logger;

        public BorrowerProfileService(IBorrowerProfileRepository repository, IUserRepository users,
            IAccountRepository accounts, IRoleRepository roles, IProfilePhotoService photos,
            IEmailManager email, ILogger<BorrowerProfileService> logger)
        {
            _repository = repository;
            this.users = users;
            this.accounts = accounts;
            this.roles = roles;
            this.photos = photos;
            this.email = email;
            this.logger = logger;
        }

        public List<Department> GetDepartments() =>
            _repository.GetActiveDepartments();

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
                OverallTotal = _repository.CountProfiles(),
                OverallEligible = _repository.CountProfiles(EligibilityStatuses.Eligible),
                OverallSuspended = _repository.CountProfiles(EligibilityStatuses.Suspended)
            };

            vm.TotalCount = _repository.CountMatchingProfiles(filterSearch, filterStatus);
            vm.TotalPages = Math.Max(1, (int)Math.Ceiling(vm.TotalCount / (double)pageSize));
            vm.Page = Math.Min(Math.Max(page, 1), vm.TotalPages);

            vm.Rows = _repository.GetPage(filterSearch, filterStatus, (vm.Page - 1) * pageSize, pageSize)
                       .Select(b => new BorrowerRowViewModel
                       {
                           UserId = b.UserId,
                           UserCode = b.User.UserCode,
                           FullName = string.Join(" ", new[] { b.User.FirstName, b.User.MiddleName, b.User.LastName, b.User.Suffix }
                                                       .Where(x => !string.IsNullOrWhiteSpace(x))),
                           ContactNumber = b.User.ContactNumber,
                           DepartmentId = b.DepartmentId,
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
            var b = _repository.GetForHistory(userId);
            if (b == null) return null;

            var reservations = _repository.GetReservations(userId);

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
            if (m.DepartmentId == 0) return (false, "Department is required.");

            var profile = _repository.GetForUpdate(m.UserId);
            if (profile == null) return (false, "Borrower not found.");

            var now = DateTime.Now;
            profile.DepartmentId = m.DepartmentId;
            profile.Program = m.Program?.Trim();
            profile.YearLevel = m.YearLevel;
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
            _repository.SaveChanges();
            return (true, "Borrower updated.");
        }

        public (bool ok, string message) SetStatus(int userId, bool suspend, string reason, DateTime? until, string actor)
        {
            var e = _repository.GetForUpdate(userId);
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
            _repository.SaveChanges();
            return (true, suspend ? "Borrower suspended." : "Borrower activated.");
        }

        public (bool ok, string message) Delete(int userId)
        {
            var e = _repository.GetForUpdate(userId);
            if (e == null) return (false, "Borrower not found.");
            if (_repository.HasReservations(userId))
                return (false, "This borrower has reservations and can't be deleted. Suspend them instead.");
            _repository.Delete(e);   // removes the borrower profile only; the User row is kept
            return (true, "Borrower profile removed. The user record was kept.");
        }

        public async Task<bool> RegisterAsync(BorrowerRegistrationViewModel model, Stream photo, string fileName, CancellationToken cancellationToken = default)
        {
            model.UserCode = model.UserCode?.Trim(); model.Email = model.Email?.Trim();
            Validator.ValidateObject(model, new ValidationContext(model), true);

            if (users.UserCodeExists(model.UserCode)) 
                throw new ValidationException("That student or employee ID is already registered.");

            if (accounts.EmailExists(model.Email)) 
                throw new ValidationException("That email is already registered.");

            if (!GetDepartments().Any(d => d.DepartmentId == model.DepartmentId)) throw new ValidationException("Select an active department.");
            var role = roles.GetRoles().SingleOrDefault(r => r.RoleName == "Borrower" && r.IsActive);
            if (role == null) throw new ValidationException("Borrower registration is currently unavailable. Contact your administrator.");
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
                    RoleId = role.RoleId,
                    Email = model.Email,
                    PasswordHash = PasswordManager.EncryptPassword(model.Password),
                    AccountStatus = AccountStatuses.PendingApproval,
                    CreatedBy = "Registration",
                    CreatedAt = now
                },
                BorrowerProfileUser = new BorrowerProfile
                {
                    DepartmentId = model.DepartmentId.Value,
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
                if (photo != null) path = await photos.UploadProfileAsync(photo, fileName, cancellationToken);
                user.ProfilePhotoStoragePath = path;
                // One SaveChanges inserts this complete related graph in an EF transaction.
                users.AddUser(user);
            }
            catch (Exception exception)
            {
                if (path != null)
                {
                    try { await photos.DeleteAsync(path, CancellationToken.None); }
                    catch (Exception cleanup) { logger.LogWarning(cleanup, "Unable to clean up registration photo after a failed save."); }
                }
                if (exception is DbUpdateException && exception.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
                    throw new ValidationException("That email or school ID was just registered. Please sign in or contact your administrator.");
                throw;
            }
            try
            {
                await email.SendAsync(model.Email, "HiRAM registration awaiting approval",
                    "Your registration has been received and is awaiting administrator approval. You cannot sign in until it is approved. Please do not register again while your request is pending.");
                return true;
            }
            catch (Exception exception) { logger.LogWarning(exception, "Registration saved but receipt email failed for user {UserId}.", user.UserId); return false; }
        }
    }
}
