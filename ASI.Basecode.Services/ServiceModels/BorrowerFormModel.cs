using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    public class BorrowerFormModel
    {
            public int UserId { get; set; }
            public int DepartmentId { get; set; }
            public string Program { get; set; }
            public byte? YearLevel { get; set; }
            public string EligibilityStatus { get; set; }
            public string SuspensionReason { get; set; }
            public DateTime? SuspendedUntil { get; set; }
    }
}
