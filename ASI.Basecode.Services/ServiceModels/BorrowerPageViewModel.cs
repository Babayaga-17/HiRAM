using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    public class BorrowerPageViewModel
    {
        public string Search { get; set; }
        public string Status { get; set; }              // "", "Eligible" or "Suspended"
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 5;
        public int TotalPages { get; set; } = 1;
        public int TotalCount { get; set; }             // rows matching the search/filter
        public int OverallTotal { get; set; }           // all borrowers (for the stat cards)
        public int OverallEligible { get; set; }
        public int OverallSuspended { get; set; }
        public List<BorrowerRowViewModel> Rows { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
    }
}
