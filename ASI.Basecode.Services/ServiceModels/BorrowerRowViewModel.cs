using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    public class BorrowerRowViewModel
    {
        public int UserId { get; set; }
        public string UserCode { get; set; }
        public string FullName { get; set; }
        public string ContactNumber { get; set; }
        public int DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public string Program { get; set; }
        public byte? YearLevel { get; set; }
        public string EligibilityStatus { get; set; }
        public string SuspensionReason { get; set; }
        public DateTime? SuspendedUntil { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
