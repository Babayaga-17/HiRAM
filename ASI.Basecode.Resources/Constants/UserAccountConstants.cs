namespace ASI.Basecode.Resources.Constants
{
    /// <summary>
    /// Role names exactly as stored in dbo.Roles.role_name (see database/002_revised_database_data.sql).
    /// These values are also the Role claim used by [Authorize] policies.
    /// </summary>
    public static class RoleNames
    {
        public const string Admin = "Admin";
        public const string Custodian = "Custodian";
        public const string Borrower = "Borrower";
    }

    /// <summary>
    /// Allowed values of dbo.Accounts.account_status (CK_Accounts_status).
    /// </summary>
    public static class AccountStatuses
    {
        public const string Active = "Active";
        public const string Inactive = "Inactive";
        public const string Suspended = "Suspended";
        public const string Disabled = "Disabled";

        public static readonly string[] All = { Active, Inactive, Suspended, Disabled };
    }

    /// <summary>
    /// dbo.AuditLogs.action values written by User and Role Management.
    /// </summary>
    public static class UserAccountAuditActions
    {
        public const string Created = "USER_ACCOUNT_CREATED";
        public const string Updated = "USER_ACCOUNT_UPDATED";
        public const string RoleChanged = "USER_ROLE_CHANGED";
        public const string Activated = "USER_ACCOUNT_ACTIVATED";
        public const string Deactivated = "USER_ACCOUNT_DEACTIVATED";
        public const string AccessReset = "USER_ACCESS_RESET";
    }
}
