using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Models
{
    public partial class Reservation
    {
        [Column("approval_remarks")]
        public string ApprovalRemarks { get; set; }
    }

}
