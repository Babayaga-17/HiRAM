using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    public class HistoryItemViewModel
    {
        public string EquipmentName { get; set; }
        public string EquipmentCode { get; set; }
        public bool IsReturned { get; set; }
    }
}
