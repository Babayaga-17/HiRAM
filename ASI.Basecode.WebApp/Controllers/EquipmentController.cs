using ASI.Basecode.Services.Exceptions;
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

[Authorize(Roles = "Custodian, Admin")]
public class EquipmentController : ControllerBase<EquipmentController>
{
    private readonly IEquipmentService _equipmentService;
    private readonly IEquipmentCategoryService _equipmentCategoryService;

    public EquipmentController(
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
        try
        {
            LoadCategories(null, true);
            return View(_equipmentService.GetEquipments());
        }
        catch (Exception)
        {
            LoadCategories(null, true);
            TempData["EquipmentLoadError"] = "Unable to load equipment. Please try again.";
            return View(new List<EquipmentListItem>());
        }
    }

    [HttpGet]
    public IActionResult Create()
    {
        LoadCategories(null);
        return View(new EquipmentFormViewModel());
    }

    [HttpPost]
    public IActionResult Create(EquipmentFormViewModel equipment)
    {
        try
        {
            if (ModelState.IsValid)
            {
                _equipmentService.AddEquipment(equipment, UserId);
                TempData["EquipmentSuccess"] = "The equipment was added.";
                return RedirectToAction(nameof(Index));
            }
        }
        catch (EquipmentValidationException exception)
        {
            foreach (var error in exception.Errors)
                ModelState.AddModelError(error.Key, error.Value);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to save equipment.");
            ModelState.AddModelError(string.Empty, "Unable to save the equipment. " + exception.GetBaseException().Message);
        }

        LoadCategories(equipment.CategoryId);
        return View(equipment);
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        try
        {
            var equipment = _equipmentService.GetEquipmentById(id);

            if (equipment == null)
                return NotFound();

            LoadCategories(equipment.CategoryId);
            return View(new EquipmentFormViewModel(equipment));
        }
        catch (Exception)
        {
            TempData["EquipmentError"] = "Unable to load the equipment. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    public IActionResult Edit(EquipmentFormViewModel equipment)
    {
        try
        {
            if (ModelState.IsValid)
            {
                if (!_equipmentService.EditEquipment(equipment, UserId))
                    return NotFound();

                TempData["EquipmentSuccess"] = "The equipment was updated.";
                return RedirectToAction(nameof(Index));
            }
        }
        catch (EquipmentValidationException exception)
        {
            foreach (var error in exception.Errors)
                ModelState.AddModelError(error.Key, error.Value);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to save equipment changes.");
            ModelState.AddModelError(string.Empty, "Unable to save the changes. " + exception.GetBaseException().Message);
        }

        LoadCategories(equipment.CategoryId);
        return View(equipment);
    }

    [HttpPost]
    public IActionResult Activate(int id)
    {
        return SetStatus(id, true);
    }

    [HttpPost]
    public IActionResult Deactivate(int id)
    {
        return SetStatus(id, false);
    }

    private IActionResult SetStatus(int id, bool isActive)
    {
        try
        {
            if (_equipmentService.SetEquipmentStatus(id, isActive, UserId))
                TempData["EquipmentSuccess"] = isActive ? "The equipment was activated." : "The equipment was deactivated.";
            else
                TempData["EquipmentError"] = "The equipment no longer exists.";
        }
        catch (EquipmentValidationException exception)
        {
            TempData["EquipmentError"] = string.Join(" ", exception.Errors.Values);
        }
        catch (Exception)
        {
            TempData["EquipmentError"] = "Unable to change the equipment status. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        try
        {
            var equipment = _equipmentService.GetEquipmentById(id);

            if (equipment == null)
            {
                TempData["EquipmentError"] = "The equipment no longer exists.";
                return RedirectToAction(nameof(Index));
            }

            if (equipment.IsActive)
            {
                TempData["EquipmentError"] =
                    "Please deactivate the equipment before deleting it.";

                return RedirectToAction(nameof(Index));
            }

            // Delete the equipment through the service.
            // This method must be implemented in IEquipmentService
            // and EquipmentService.
            _equipmentService.DeleteEquipment(id, UserId);

            TempData["EquipmentSuccess"] =
                "The equipment was deleted successfully.";
        }
        catch (EquipmentValidationException exception)
        {
            TempData["EquipmentError"] =
                string.Join(" ", exception.Errors.Values);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to delete equipment.");

            TempData["EquipmentError"] =
                "Unable to delete the equipment. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    // Builds Category dropdown. The form lists active categories (plus the item's current one);
    // the Index filter lists all categories.
    private void LoadCategories(int? currentCategoryId, bool includeInactive = false)
    {
        var categories = _equipmentCategoryService.GetEquipmentCategories()
            .Where(category => includeInactive || category.IsActive || category.CategoryId == currentCategoryId)
            .OrderBy(category => category.CategoryName)
            .Select(category => new
            {
                category.CategoryId,
                Name = category.IsActive ? category.CategoryName : category.CategoryName + " (inactive)"
            })
            .ToList();

        ViewBag.Categories = new SelectList(categories, "CategoryId", "Name");
    }
}