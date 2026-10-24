using ASI.Basecode.Resources.Constants;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace ASI.Basecode.WebApp.Authorization
{
    /// <summary>
    /// WBS 5.19 Manage Menu/Function Access (role-based; HiramDb has no permission table).
    /// Single source of truth for which roles may open each menu:
    ///  - Startup.Auth registers an authorization policy for every entry that has a policy name,
    ///    and controllers use [Authorize(Policy = ...)] so access is enforced on the server.
    ///  - _AppLayout uses CanAccess() so the sidebar only shows menus the user may open.
    ///  - The Role access screen displays this table.
    /// Roles == null means "any signed-in user" (the module did not restrict it further).
    /// </summary>
    public static class MenuAccess
    {
        public const string UserManagementPolicy = "MenuAccess.UserManagement";

        public static readonly IReadOnlyList<MenuAccessEntry> Entries = new List<MenuAccessEntry>
        {
            new MenuAccessEntry(
                "UserAccount",
                "Users & roles",
                "Create, view and edit user accounts, assign or change roles, activate or deactivate accounts, and reset access.",
                UserManagementPolicy,
                new[] { RoleNames.Admin }),
            new MenuAccessEntry(
                "RoleAccess",
                "Role access",
                "View which menus and functions each role can use.",
                UserManagementPolicy,
                new[] { RoleNames.Admin }),
            // The entries below are enforced by their own controllers ([Authorize(Roles = ...)]);
            // they are listed here so the sidebar and the Role access screen show the same rules.
            new MenuAccessEntry(
                "Dashboard",
                "Dashboard",
                "Role-based home page.",
                null,
                new[] { RoleNames.Borrower, RoleNames.Custodian, RoleNames.Admin }),
            new MenuAccessEntry(
                "Equipment",
                "Equipment",
                "Equipment items management.",
                null,
                new[] { RoleNames.Custodian, RoleNames.Admin }),
            new MenuAccessEntry(
                "EquipmentCategory",
                "Equipment categories",
                "Equipment category management.",
                null,
                new[] { RoleNames.Custodian, RoleNames.Admin }),
        };

        public static MenuAccessEntry Find(string key)
        {
            return Entries.FirstOrDefault(entry => entry.Key == key);
        }

        public static bool CanAccess(ClaimsPrincipal user, string key)
        {
            if (user?.Identity?.IsAuthenticated != true)
                return false;

            var entry = Find(key);
            return entry != null && (entry.Roles == null || entry.Roles.Any(user.IsInRole));
        }
    }

    public class MenuAccessEntry
    {
        public MenuAccessEntry(string key, string label, string description, string policyName, string[] roles)
        {
            Key = key;
            Label = label;
            Description = description;
            PolicyName = policyName;
            Roles = roles;
        }

        public string Key { get; }
        public string Label { get; }
        public string Description { get; }
        public string PolicyName { get; }
        public IReadOnlyList<string> Roles { get; }

        public bool AllowsRole(string roleName)
        {
            return Roles == null || Roles.Contains(roleName);
        }
    }
}
