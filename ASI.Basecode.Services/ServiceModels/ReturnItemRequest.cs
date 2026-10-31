using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    public class ReturnItemRequest
    {
        public int ReservationItemId { get; set; }

        public bool Receive { get; set; }

        public string ConditionAtReturn { get; set; }

        [StringLength(500)]
        public string ReturnRemarks { get; set; }

        [StringLength(500)]
        public string DamageNotes { get; set; }

        [StringLength(500)]
        public string MissingPartsNotes { get; set; }
    }
}
