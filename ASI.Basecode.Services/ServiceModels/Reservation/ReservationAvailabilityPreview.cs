using System;
using System.Collections.Generic;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.ServiceModels.Reservation;

public class ReservationAvailabilityPreview
{
    public DateTime Pickup { get; set; }
    public DateTime ExpectedReturn { get; set; }
    public DateTime RangeStart { get; set; }
    public DateTime RangeEnd { get; set; }


    public List<ReservationEquipmentPreview> Items { get; set; } = new();
}

public class ReservationEquipmentPreview
{
    public int EquipmentId { get; set; }
    public bool IsAvailable { get; set; }
    public string Reason { get; set; }

    public List<EquipmentBusyPeriod> Periods { get; set; } = new();
}
