// ASI.Basecode.Services/Services/BorrowerProfileService.cs
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Services.Services
{
   

    public interface IBorrowerProfileService
    {
        BorrowerPageViewModel GetPage(string search, string status, int page, int pageSize);
        List<Department> GetDepartments();
        BorrowerHistoryViewModel GetHistory(int userId);
        (bool ok, string message) Save(BorrowerFormModel m, string actor);
        (bool ok, string message) SetStatus(int userId, bool suspend, string reason, DateTime? until, string actor);
        (bool ok, string message) Delete(int userId);
    }

    public class BorrowerProfileService : IBorrowerProfileService
    {
        private readonly HiramDbContext _db;
        public BorrowerProfileService(HiramDbContext db) => _db = db;

        public List<Department> GetDepartments() =>
            _db.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.DepartmentName).ToList();

        public BorrowerPageViewModel GetPage(string search, string status, int page, int pageSize)
        {
            if (pageSize < 1) pageSize = 5;
            var all = _db.BorrowerProfiles.AsNoTracking();

            var vm = new BorrowerPageViewModel
            {
                Search = search,
                Status = status,
                PageSize = pageSize,
                OverallTotal = all.Count(),
                OverallEligible = all.Count(b => b.EligibilityStatus == "Eligible"),
                OverallSuspended = all.Count(b => b.EligibilityStatus == "Suspended")
            };

            var q = all.Include(b => b.User).Include(b => b.Department).AsQueryable();

            if (status == "Eligible" || status == "Suspended")
                q = q.Where(b => b.EligibilityStatus == status);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(b => b.User.FirstName.Contains(s) || b.User.LastName.Contains(s)
                              || b.User.UserCode.Contains(s) || b.Program.Contains(s)
                              || b.Department.DepartmentName.Contains(s));
            }

            vm.TotalCount = q.Count();
            vm.TotalPages = Math.Max(1, (int)Math.Ceiling(vm.TotalCount / (double)pageSize));
            vm.Page = Math.Min(Math.Max(page, 1), vm.TotalPages);

            vm.Rows = q.OrderBy(b => b.User.LastName).ThenBy(b => b.User.FirstName).ThenBy(b => b.UserId)
                       .Skip((vm.Page - 1) * pageSize).Take(pageSize)
                       .ToList()
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
            var b = _db.BorrowerProfiles.AsNoTracking().Include(x => x.User).FirstOrDefault(x => x.UserId == userId);
            if (b == null) return null;

            var reservations = _db.Reservations.AsNoTracking()
                .Where(r => r.BorrowerUserId == userId)
                .Include(r => r.ReservationItems).ThenInclude(i => i.Equipment)
                .OrderByDescending(r => r.RequestedAt)
                .ToList();

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

            var profile = _db.BorrowerProfiles.FirstOrDefault(x => x.UserId == m.UserId);
            if (profile == null) return (false, "Borrower not found.");

            var now = DateTime.Now;
            profile.DepartmentId = m.DepartmentId;
            profile.Program = m.Program?.Trim();
            profile.YearLevel = m.YearLevel;
            profile.EligibilityStatus = m.EligibilityStatus == "Suspended" ? "Suspended" : "Eligible";

            if (profile.EligibilityStatus == "Suspended")
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
            _db.SaveChanges();
            return (true, "Borrower updated.");
        }

        public (bool ok, string message) SetStatus(int userId, bool suspend, string reason, DateTime? until, string actor)
        {
            var e = _db.BorrowerProfiles.FirstOrDefault(b => b.UserId == userId);
            if (e == null) return (false, "Borrower not found.");
            var now = DateTime.Now;
            if (suspend)
            {
                e.EligibilityStatus = "Suspended";
                e.SuspensionReason = reason?.Trim();
                e.SuspendedUntil = until;
                e.SuspendedAt = now;
            }
            else
            {
                e.EligibilityStatus = "Eligible";
                e.SuspensionReason = null;
                e.SuspendedUntil = null;
                e.SuspendedAt = null;
                e.SuspendedByUserId = null;
            }
            e.UpdatedBy = actor; e.UpdatedAt = now;
            _db.SaveChanges();
            return (true, suspend ? "Borrower suspended." : "Borrower activated.");
        }

        public (bool ok, string message) Delete(int userId)
        {
            var e = _db.BorrowerProfiles.Include(b => b.Reservations).FirstOrDefault(b => b.UserId == userId);
            if (e == null) return (false, "Borrower not found.");
            if (e.Reservations.Any())
                return (false, "This borrower has reservations and can't be deleted. Suspend them instead.");
            _db.BorrowerProfiles.Remove(e);   // removes the borrower profile only; the User row is kept
            _db.SaveChanges();
            return (true, "Borrower profile removed. The user record was kept.");
        }
    }
}
