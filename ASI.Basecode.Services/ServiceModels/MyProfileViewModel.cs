namespace ASI.Basecode.Services.ServiceModels
{
    public class MyProfileViewModel
    {
        public string UserCode { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string ContactNumber { get; set; }
        public string RoleName { get; set; }
        public string AccountStatus { get; set; }
        public string ProfilePhotoUrl { get; set; }
        public bool HasBorrowerProfile { get; set; }
        public string BorrowerType { get; set; }
        public string DepartmentName { get; set; }
        public string Program { get; set; }
        public byte? YearLevel { get; set; }
        public string EligibilityStatus { get; set; }
        public string SuspensionReason { get; set; }
        public System.DateTime? SuspendedUntil { get; set; }
    }
}
