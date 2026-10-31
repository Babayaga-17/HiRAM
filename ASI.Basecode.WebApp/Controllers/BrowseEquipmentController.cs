using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace ASI.Basecode.WebApp.Controllers;

[Authorize(Roles = "Borrower")]
public class BrowseEquipmentController : ControllerBase<BrowseEquipmentController>
{
    private const string SelectionKey = "SelectedEquipmentIds";

    private readonly IEquipmentService _equipmentService;
    private readonly IEquipmentCategoryService _equipmentCategoryService;
    private readonly IEquipmentReservationService _reservationService;

    public BrowseEquipmentController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IEquipmentService equipmentService,
        IEquipmentCategoryService equipmentCategoryService,
        IEquipmentReservationService reservationService,
        IMapper mapper = null) : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _equipmentService = equipmentService;
        _equipmentCategoryService = equipmentCategoryService;
        _reservationService = reservationService;
    }

    [HttpGet]
    public IActionResult Index(int? reservationId)
    {
        if (!CanEditSelection(reservationId))
            return NotFound();

        ViewData["EditingReservationId"] = reservationId;
        ViewBag.Categories = new SelectList(new List<EquipmentCategoryListItem>(), "CategoryId", "CategoryName");
        ViewBag.SelectedIds = new List<int>();

        try
        {
            LoadCategories();

            var equipments = _equipmentService.GetBorrowableEquipments();

            // Drop selections that can no longer be borrowed (deactivated, damaged, or lost).
            var selectableIds = equipments
                .Where(item => item.Status != EquipmentAvailability.Unavailable)
                .Select(item => item.EquipmentId)
                .ToList();

            var selected = GetSelectedIds(reservationId).Where(selectableIds.Contains).ToList();
            SaveSelectedIds(selected, reservationId);
            ViewBag.SelectedIds = selected;

            return View(equipments);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load equipment for browsing.");
            TempData["BrowseLoadError"] = "Unable to load equipment. Please try again.";
            return View(new List<EquipmentListItem>());
        }
    }

    [HttpGet]
    public IActionResult Details(int id, int? reservationId)
    {
        if (!CanEditSelection(reservationId))
            return NotFound();

        var equipment = _equipmentService.GetBorrowableEquipmentById(id);
        if (equipment == null)
            return NotFound();

        ViewData["EditingReservationId"] = reservationId;
        ViewBag.IsSelected = GetSelectedIds(reservationId).Contains(id);
        return View(equipment);
    }

    [HttpGet]
    public IActionResult Schedule(int id, DateTime? from, DateTime? to, int? reservationId)
    {
        if (!CanEditSelection(reservationId))
            return NotFound();
        if (!from.HasValue || !to.HasValue || to <= from || (to.Value - from.Value).TotalDays > 62)
            return BadRequest(new { message = "Choose a calendar range of up to 62 days." });

        try
        {
            if (_equipmentService.GetBorrowableEquipmentById(id) == null)
                return NotFound();

            var periods = _equipmentService.GetSchedule(id, from.Value, to.Value, reservationId);
            return Json(new
            {
                periods = periods.Select(period => new
                {
                    type = period.Type,
                    label = period.Label,
                    startIso = period.Start.ToString("yyyy-MM-dd'T'HH:mm:ss"),
                    endIso = period.End.ToString("yyyy-MM-dd'T'HH:mm:ss")
                })
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load equipment schedule.");
            return StatusCode(500, new { message = "Unable to load the calendar. Please try again." });
        }
    }

    [HttpGet]
    public IActionResult Availability(int id, DateTime? from, DateTime? to, int? reservationId)
    {
        if (!CanEditSelection(reservationId))
            return NotFound();

        try
        {
            var start = from ?? DateTime.Now;
            var end = to ?? start.AddDays(14);
            var result = _equipmentService.GetAvailability(id, start, end, reservationId);

            return Json(new
            {
                isAvailable = result.IsAvailable,
                reason = result.Reason,
                periods = result.Periods.Select(period => new
                {
                    type = period.Type,
                    label = period.Label,
                    start = FormatDate(period.Start),
                    end = FormatDate(period.End)
                }),
                slots = result.Slots.Select(slot => new
                {
                    start = FormatDate(slot.Start),
                    end = FormatDate(slot.End)
                })
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load equipment availability.");
            return StatusCode(500, new { message = "Unable to load availability." });
        }
    }

    // Select equipment (kept in the session until reservations are built)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Select(int id, int? reservationId)
    {
        if (!CanEditSelection(reservationId))
            return NotFound();

        try
        {
            var equipment = _equipmentService.GetBorrowableEquipmentById(id);

            if (equipment == null)
                return BadRequest(new { message = "This equipment is no longer available for borrowing." });

            if (!equipment.IsActive)
                return BadRequest(new { message = "This equipment is inactive and cannot be selected." });

            if (equipment.Status == EquipmentAvailability.Unavailable)
                return BadRequest(new { message = "Equipment in " + equipment.ConditionStatus + " condition cannot be selected." });

            var selected = GetSelectedIds(reservationId);

            if (!selected.Contains(id))
            {
                selected.Add(id);
                SaveSelectedIds(selected, reservationId);
            }

            return Json(new { ids = selected });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to select equipment.");
            return StatusCode(500, new { message = "Unable to select the equipment. Please try again." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Deselect(int id, int? reservationId)
    {
        if (!CanEditSelection(reservationId))
            return NotFound();

        var selected = GetSelectedIds(reservationId);
        selected.Remove(id);
        SaveSelectedIds(selected, reservationId);

        return Json(new { ids = selected });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Clear(int? reservationId)
    {
        if (!CanEditSelection(reservationId))
            return NotFound();

        SaveSelectedIds(new List<int>(), reservationId);

        return Json(new { ids = new List<int>() });
    }

    private void LoadCategories()
    {
        var categories = _equipmentCategoryService.GetEquipmentCategories()
            .OrderBy(category => category.CategoryName)
            .ToList();

        ViewBag.Categories = new SelectList(categories, "CategoryId", "CategoryName");
    }

    private List<int> GetSelectedIds(int? reservationId)
    {
        var key = reservationId.HasValue
            ? $"SelectedEquipmentIds:Edit:{reservationId.Value}"
            : SelectionKey;

        var value = _session.GetString(key);

        if (string.IsNullOrEmpty(value))
            return new List<int>();

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();
    }

    private void SaveSelectedIds(List<int> ids, int? reservationId)
    {
        var key = reservationId.HasValue
            ? $"SelectedEquipmentIds:Edit:{reservationId.Value}"
            : SelectionKey;

        _session.SetString(key, string.Join(",", ids));
    }

    private static string FormatDate(DateTime value)
    {
        return value.ToString("MMM d, yyyy h:mm tt");
    }

    private bool CanEditSelection(int? reservationId)
    {
        if (!reservationId.HasValue)
            return true;

        if (!int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var borrowerUserId))
            return false;

        var reservation = _reservationService.GetReservationDetails(
            reservationId.Value,
            borrowerUserId);

        return reservation?.Status == "Pending";
    }
}
