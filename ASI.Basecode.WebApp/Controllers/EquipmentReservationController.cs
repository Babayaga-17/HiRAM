using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ASI.Basecode.WebApp.Controllers;

[Authorize(Roles = "Borrower")]
public class EquipmentReservationController : ControllerBase<EquipmentReservationController>
{
    private const string SelectionKey = "SelectedEquipmentIds";

    private readonly IEquipmentService _equipmentService;
    private readonly IEquipmentReservationService _reservationService;

    public EquipmentReservationController(
                            IHttpContextAccessor httpContextAccessor, 
                            ILoggerFactory loggerFactory, 
                            IConfiguration configuration,
                            IEquipmentService equipmentService,
                            IEquipmentReservationService reservationService,
                            IMapper mapper = null) : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _equipmentService = equipmentService;
        _reservationService = reservationService;
    }

    [HttpGet]
    public IActionResult Create()
    {
        var selection = HttpContext.Session.GetString(SelectionKey);

        var selectedIds = (selection ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => int.TryParse(value, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        var model = new ReservationFormModel { EquipmentIds = selectedIds };
        LoadEquipment(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create([Bind("EquipmentIds,Pickup,ExpectedReturn,Purpose")] ReservationFormModel model)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var borrowerUserId))
            return Challenge();

        if (ModelState.IsValid)
        {
            try
            {
                var result = _reservationService.Submit(model, borrowerUserId);
                if (result.Error == null)
                {
                    HttpContext.Session.Remove(SelectionKey);
                    TempData["ReservationNumber"] = result.ReservationNumber;
                    return RedirectToAction(nameof(Submitted));
                }
                ModelState.AddModelError(string.Empty, result.Error);
            }
            catch (Exception)
            {
                //_logger.LogError(exception, "Unable to submit equipment reservation for borrower {UserId}.", borrowerUserId);
                ModelState.AddModelError(string.Empty,
                    "Your reservation could not be completed. Please try again; availability will be checked again.");
            }
        }

        LoadEquipment(model);
        return View(model);
    }

    [HttpGet]
    public IActionResult Submitted()
    {
        var number = TempData["ReservationNumber"] as string;
        if (number == null) return RedirectToAction("Index", "BrowseEquipment");
        return View(model: number);
    }

    private void LoadEquipment(ReservationFormModel model)
    {
        model.Equipment = new List<EquipmentListItem>();
        foreach (var id in (model.EquipmentIds ?? new List<int>()).Distinct())
        {
            var item = _equipmentService.GetBorrowableEquipmentById(id);
            if (item == null || item.Status == EquipmentAvailability.Unavailable)
            {
                ViewData["SelectionError"] = "Some selected equipment can no longer be borrowed. Return to Browse Equipment and review your selection.";
                continue;
            }
            model.Equipment.Add(item);
        }
    }
}
