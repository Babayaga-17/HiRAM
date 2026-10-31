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

[Authorize(Roles = "Borrower, Custodian, Admin")]
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
    public IActionResult Index()
    {
        ViewBag.Categories = new SelectList(new List<EquipmentCategoryListItem>(), "CategoryId", "CategoryName");

        try
        {
            var categories = _equipmentCategoryService.GetEquipmentCategories()
                .OrderBy(category => category.CategoryName)
                .ToList();
            ViewBag.Categories = new SelectList(categories, "CategoryId", "CategoryName");

            var equipments = _equipmentService.GetEquipments()
                .Where(item => item.IsActive)
                .ToList();

            return View(equipments);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load the availability calendar.");
            TempData["CalendarLoadError"] = "Unable to load the calendar. Please try again.";
            return View(new List<EquipmentListItem>());
        }
    }

    // The page asks for one visible range at a time (a day, a week, or a month grid).
    [HttpGet]
    public IActionResult Events(DateTime start, DateTime end)
    {
        try
        {
            var events = _equipmentService.GetCalendarEvents(start, end);

            return Json(events.Select(item => new
            {
                equipmentId = item.EquipmentId,
                code = item.EquipmentCode,
                name = item.EquipmentName,
                categoryId = item.CategoryId,
                type = item.Type,
                status = item.Status,
                start = item.Start.ToString("yyyy-MM-ddTHH:mm:ss"),
                end = item.End.ToString("yyyy-MM-ddTHH:mm:ss")
            }));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load calendar events.");
            return StatusCode(500, new { message = "Unable to load the calendar. Please try again." });
        }
    }
}