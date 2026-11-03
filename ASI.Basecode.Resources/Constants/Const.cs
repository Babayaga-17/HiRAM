namespace ASI.Basecode.Resources.Constants
{
    /// <summary>
    /// Class for variables with constant values
    /// </summary>
    public class Const
    {
        /// <summary>
        ///API result success
        /// </summary>
        public const string ApiResultSuccess = "Success";

        /// <summary>
        ///API result error
        /// </summary>
        public const string ApiResultError = "Error";

        /// <summary>
        /// System
        /// </summary>
        public const string System = "sys";

        /// <summary>
        /// Api Key Header Name
        /// </summary>
        public const string ApiKey = "X-Basecode-API-Key";

        /// <summary>
        /// authentication scheme Name
        /// </summary>
        public const string AuthenticationScheme = "ASI_Basecode";

        /// <summary>
        /// authentication Issuer
        /// </summary>
        public const string Issuer = "asi.basecode";
    }

    /// <summary>
    /// Allowed values of dbo.Reservations.status (CK_Reservations_status).
    /// These are the statuses that are actually stored. "All" (a UI filter) is in
    /// <see cref="ReservationFilters"/>, and the computed "Overdue" shown in reports is in
    /// <see cref="ReservationDisplayStatuses"/>.
    /// </summary>
    public static class ReservationStatuses
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Borrowed = "Borrowed";
        public const string Returned = "Returned";
        public const string Overdue = "Overdue";
        public const string Cancelled = "Cancelled";

        /// <summary>Every stored status.</summary>
        public static readonly string[] Values = { Pending, Approved, Rejected, Borrowed, Returned, Overdue, Cancelled };

        /// <summary>Statuses that hold the equipment, so no one else can reserve the same time.</summary>
        public static readonly string[] Blocking = { Pending, Approved, Borrowed, Overdue };
    }

    /// <summary>
    /// UI-only filter values. These are never stored in the database.
    /// </summary>
    public static class ReservationFilters
    {
        public const string All = "All";
    }

    /// <summary>
    /// Statuses that reports calculate while building a row. The value is the same text as the
    /// stored status, but here it is worked out from the due date and not read from the database.
    /// </summary>
    public static class ReservationDisplayStatuses
    {
        public const string Overdue = ReservationStatuses.Overdue;
    }

    /// <summary>
    /// Allowed values of dbo.Equipments.condition_status and of the condition columns of
    /// dbo.ReservationItems (CK_Equipments_condition, CK_ReservationItems_condition_*).
    /// </summary>
    public static class EquipmentConditions
    {
        public const string Excellent = "Excellent";
        public const string Good = "Good";
        public const string Fair = "Fair";
        public const string Damaged = "Damaged";
        public const string Lost = "Lost";

        /// <summary>Every condition, in the order shown in forms.</summary>
        public static readonly string[] Values = { Excellent, Good, Fair, Damaged, Lost };

        /// <summary>Conditions that allow the equipment to be borrowed.</summary>
        public static readonly string[] Borrowable = { Excellent, Good, Fair };
    }

    /// <summary>
    /// Allowed values of dbo.BorrowerProfiles.eligibility_status (CK_BorrowerProfiles_eligibility).
    /// </summary>
    public static class EligibilityStatuses
    {
        public const string Eligible = "Eligible";
        public const string Ineligible = "Ineligible";
        public const string Suspended = "Suspended";
    }

    /// <summary>
    /// Allowed values of dbo.MaintenanceRecords.status (CK_MaintenanceRecords_status).
    /// </summary>
    public static class MaintenanceStatuses
    {
        public const string Scheduled = "Scheduled";
        public const string InProgress = "InProgress";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
    }

    /// <summary>
    /// Type of a busy period returned by the availability checks and the calendar.
    /// The browser scripts compare against these same texts.
    /// </summary>
    public static class AvailabilityPeriodTypes
    {
        public const string Reserved = "Reserved";
        public const string Borrowed = "Borrowed";
        public const string Maintenance = "Maintenance";
    }
}
