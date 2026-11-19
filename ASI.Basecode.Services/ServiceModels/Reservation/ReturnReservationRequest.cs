using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels.Reservation
{
    public class ReturnReservationRequest
    {
        public int ReservationId { get; set; }

        [Required]
        public string RowVersion { get; set; }

        public List<ReturnItemRequest> Items { get; set; } = new();
    }
}
