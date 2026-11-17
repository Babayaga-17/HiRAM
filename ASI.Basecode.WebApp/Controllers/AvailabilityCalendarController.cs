using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.ServiceModels.Equipment;
using ASI.Basecode.WebApp.Models;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.WebApp.Controllers;

[Authorize(Roles = $"{RoleNames.Borrower}, {RoleNames.Custodian}, {RoleNames.Admin}")]
public class AvailabilityCalendarController : ControllerBase<AvailabilityCalendarController>
{
    private readonly IEquipmentService _equipmentService;
    private readonly IEquipmentCategoryService _equipmentCategoryService;

    public AvailabilityCalendarController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IEquipmentService equipmentService,
        IEquipmentCategoryService equipmentCategoryService,
        IMapper mapper = null) : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _equipmentService = equipmentService;
        _equipmentCategoryService = equipmentCategoryService;
    }

    [HttpGet]
    public IActionResult Index(
        string search = null,
        int? equipmentId = null,
        int? categoryId = null,
        string eventType = null,
        string viewMode = "month",
        DateTime? date = null)
    {
        var selectedDate = date.HasValue ? date.Value.Date : DateTime.Today;
        viewMode = (viewMode ?? "month").ToLowerInvariant();
        if (viewMode != "day" && viewMode != "week" && viewMode != "month")
        {
            viewMode = "month";
        }

        try
        {
            var categories = _equipmentCategoryService.GetEquipmentCategories()
                .OrderBy(c => c.CategoryName)
                .ToList();

            var allEquipments = _equipmentService.GetEquipments()
                .Where(item => item.IsActive)
                .ToList();

            DateTime rangeStart;
            DateTime rangeEnd;
            DateTime prevDate;
            DateTime nextDate;
            string rangeTitle;

            if (viewMode == "day")
            {
                rangeStart = selectedDate.Date;
                rangeEnd = selectedDate.Date.AddDays(1).AddTicks(-1);
                prevDate = selectedDate.AddDays(-1);
                nextDate = selectedDate.AddDays(1);
                rangeTitle = selectedDate.ToString("MMMM d, yyyy");
            }
            else if (viewMode == "week")
            {
                int diff = (7 + (int)selectedDate.DayOfWeek - (int)DayOfWeek.Sunday) % 7;
                rangeStart = selectedDate.AddDays(-diff).Date;
                rangeEnd = rangeStart.AddDays(7).AddTicks(-1);
                prevDate = selectedDate.AddDays(-7);
                nextDate = selectedDate.AddDays(7);
                var weekEnd = rangeStart.AddDays(6);
                rangeTitle = rangeStart.Month == weekEnd.Month
                    ? $"{rangeStart:MMM d} – {weekEnd:d, yyyy}"
                    : $"{rangeStart:MMM d} – {weekEnd:MMM d, yyyy}";
            }
            else // month
            {
                var firstOfMonth = new DateTime(selectedDate.Year, selectedDate.Month, 1);
                var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);
                int startDiff = (7 + (int)firstOfMonth.DayOfWeek - (int)DayOfWeek.Sunday) % 7;
                rangeStart = firstOfMonth.AddDays(-startDiff).Date;

                int endDiff = (6 - (int)lastOfMonth.DayOfWeek + 7) % 7;
                rangeEnd = lastOfMonth.AddDays(endDiff).Date.AddDays(1).AddTicks(-1);

                prevDate = selectedDate.AddMonths(-1);
                nextDate = selectedDate.AddMonths(1);
                rangeTitle = selectedDate.ToString("MMMM yyyy");
            }

            var allEventsInRange = _equipmentService.GetCalendarEvents(rangeStart, rangeEnd);

            var dayStart = selectedDate.Date;
            var dayEnd = dayStart.AddDays(1);
            var dayEvents = _equipmentService.GetCalendarEvents(dayStart, dayEnd);

            var filteredEquipments = allEquipments.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                filteredEquipments = filteredEquipments.Where(e =>
                    (!string.IsNullOrEmpty(e.EquipmentName) && e.EquipmentName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(e.EquipmentCode) && e.EquipmentCode.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(e.Brand) && e.Brand.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(e.Model) && e.Model.Contains(term, StringComparison.OrdinalIgnoreCase)));
            }
            if (equipmentId.HasValue && equipmentId.Value > 0)
            {
                filteredEquipments = filteredEquipments.Where(e => e.EquipmentId == equipmentId.Value);
            }
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                filteredEquipments = filteredEquipments.Where(e => e.CategoryId == categoryId.Value);
            }
            var filteredList = filteredEquipments.ToList();
            var matchingEquipmentIds = filteredList.Select(e => e.EquipmentId).ToHashSet();

            var events = allEventsInRange.Where(e => matchingEquipmentIds.Contains(e.EquipmentId)).ToList();
            var selectedDateEvents = dayEvents.Where(e => matchingEquipmentIds.Contains(e.EquipmentId)).ToList();

            int reservedCount = events.Count(e => e.Type.Equals("Reserved", StringComparison.OrdinalIgnoreCase));
            int borrowedCount = events.Count(e => e.Type.Equals("Borrowed", StringComparison.OrdinalIgnoreCase));
            int maintenanceCount = events.Count(e => e.Type.Equals("Maintenance", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(eventType) && !eventType.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                events = events.Where(e => e.Type.Equals(eventType, StringComparison.OrdinalIgnoreCase)).ToList();
                selectedDateEvents = selectedDateEvents.Where(e => e.Type.Equals(eventType, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var busyOnSelectedDay = dayEvents
                .Where(e => e.Start < dayEnd && e.End > dayStart)
                .Select(e => e.EquipmentId)
                .Distinct()
                .ToHashSet();

            var borrowableConditions = EquipmentConditions.Borrowable;
            var availableEquipments = filteredList
                .Where(e => !busyOnSelectedDay.Contains(e.EquipmentId) && e.IsActive && borrowableConditions.Contains(e.ConditionStatus))
                .OrderBy(e => e.EquipmentName)
                .ToList();

            var model = new AvailabilityCalendarViewModel
            {
                Search = search,
                EquipmentId = equipmentId,
                CategoryId = categoryId,
                EventType = eventType,
                ViewMode = viewMode,
                SelectedDate = selectedDate,
                RangeStart = rangeStart,
                RangeEnd = rangeEnd,
                RangeTitle = rangeTitle,
                PrevDate = prevDate,
                NextDate = nextDate,
                AllEquipments = allEquipments,
                Categories = categories,
                Events = events,
                ReservedCount = reservedCount,
                BorrowedCount = borrowedCount,
                MaintenanceCount = maintenanceCount,
                AvailableEquipments = availableEquipments,
                SelectedDateEvents = selectedDateEvents,
                EquipmentSchedule = events.OrderBy(e => e.Start).ToList()
            };

            return View(model);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load the availability calendar.");
            TempData["CalendarLoadError"] = "Unable to load the calendar. Please try again.";
            return View(new AvailabilityCalendarViewModel());
        }
    }
}
