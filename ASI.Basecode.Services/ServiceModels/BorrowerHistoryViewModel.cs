using ASI.Basecode.Services.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    public class BorrowerHistoryViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string UserCode { get; set; }
        public List<HistoryRowViewModel> Rows { get; set; } = new();
    }

}
