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

namespace ASI.Basecode.WebApp.Controllers;

[Authorize(Roles = "Borrower")]
public class BrowseEquipmentController : ControllerBase<BrowseEquipmentController>
{
    private const string SelectionKey = "SelectedEquipmentIds";

    private readonly IEquipmentService _equipmentService;
    private readonly IEquipmentCategoryService _equipmentCategoryService;

    public BrowseEquipmentController(
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
    public IActionResult Index()
    {
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

            var selected = GetSelectedIds().Where(selectableIds.Contains).ToList();
            SaveSelectedIds(selected);
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
    public IActionResult Availability(int id, DateTime? from, DateTime? to)
    {
        try
        {
            var start = from ?? DateTime.Now;
            var end = to ?? start.AddDays(14);
            var result = _equipmentService.GetAvailability(id, start, end);

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
    public IActionResult Select(int id)
    {
        try
        {
            var equipment = _equipmentService.GetBorrowableEquipmentById(id);

            if (equipment == null)
                return BadRequest(new { message = "This equipment is no longer available for borrowing." });

            if (!equipment.IsActive)
                return BadRequest(new { message = "This equipment is inactive and cannot be selected." });

            if (equipment.Status == EquipmentAvailability.Unavailable)
                return BadRequest(new { message = "Equipment in " + equipment.ConditionStatus + " condition cannot be selected." });

            var selected = GetSelectedIds();

            if (!selected.Contains(id))
            {
                selected.Add(id);
                SaveSelectedIds(selected);
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
    public IActionResult Deselect(int id)
    {
        var selected = GetSelectedIds();
        selected.Remove(id);
        SaveSelectedIds(selected);

        return Json(new { ids = selected });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Clear()
    {
        SaveSelectedIds(new List<int>());

        return Json(new { ids = new List<int>() });
    }

    private void LoadCategories()
    {
        var categories = _equipmentCategoryService.GetEquipmentCategories()
            .OrderBy(category => category.CategoryName)
            .ToList();

        ViewBag.Categories = new SelectList(categories, "CategoryId", "CategoryName");
    }

    private List<int> GetSelectedIds()
    {
        var value = _session.GetString(SelectionKey);

        if (string.IsNullOrEmpty(value))
            return new List<int>();

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();
    }

    private void SaveSelectedIds(List<int> ids)
    {
        _session.SetString(SelectionKey, string.Join(",", ids));
    }

    private static string FormatDate(DateTime value)
    {
        return value.ToString("MMM d, yyyy h:mm tt");
    }
}