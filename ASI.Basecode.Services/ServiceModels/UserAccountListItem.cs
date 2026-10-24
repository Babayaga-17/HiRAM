using System;
using System.Linq;

namespace ASI.Basecode.Services.ServiceModels
{
    /// <summary>
    /// A user with their account, used by the list, details and edit screens.
    /// </summary>
    public class UserAccountListItem
    {
        public int UserId { get; set; }
        public int AccountId { get; set; }
        public string UserCode { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string MiddleName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Suffix { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public byte RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string AccountStatus { get; set; } = string.Empty;
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockoutEndAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
        public DateTime? UpdatedAt { get; set; }

        /// <summary>Concurrency token sent back by edit/status forms (Accounts.updated_at ticks, or 0).</summary>
        public string Version => UpdatedAt.HasValue ? UpdatedAt.Value.Ticks.ToString() : "0";

        public string FullName
        {
            get
            {
                var middleInitial = string.IsNullOrWhiteSpace(MiddleName) ? null : MiddleName.Trim().Substring(0, 1) + ".";
                return string.Join(" ", new[] { FirstName, middleInitial, LastName, Suffix }.Where(part => !string.IsNullOrWhiteSpace(part)));
            }
        }

        public bool IsActive => AccountStatus == Resources.Constants.AccountStatuses.Active;
    }

    public class RoleListItem
    {
        public byte RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int ActiveAccountCount { get; set; }
        public int TotalAccountCount { get; set; }
    }
}
