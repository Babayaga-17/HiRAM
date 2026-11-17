using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.ServiceModels.Equipment;
using System;
using System.Collections.Generic;

namespace ASI.Basecode.WebApp.Models;

public class AvailabilityCalendarViewModel
{
    public string Search { get; set; }
    public int? EquipmentId { get; set; }
    public int? CategoryId { get; set; }
    public string EventType { get; set; }
    public string ViewMode { get; set; } = "month"; // "month", "week", "day"
    public DateTime SelectedDate { get; set; } = DateTime.Today;

    public DateTime RangeStart { get; set; }
    public DateTime RangeEnd { get; set; }
    public string RangeTitle { get; set; }

    public DateTime PrevDate { get; set; }
    public DateTime NextDate { get; set; }

    public List<EquipmentListItem> AllEquipments { get; set; } = new();
    public List<EquipmentCategoryListItem> Categories { get; set; } = new();

    public List<EquipmentCalendarEvent> Events { get; set; } = new();

    public int ReservedCount { get; set; }
    public int BorrowedCount { get; set; }
    public int MaintenanceCount { get; set; }

    public List<EquipmentListItem> AvailableEquipments { get; set; } = new();
    public List<EquipmentCalendarEvent> SelectedDateEvents { get; set; } = new();
    public List<EquipmentCalendarEvent> EquipmentSchedule { get; set; } = new();
}
