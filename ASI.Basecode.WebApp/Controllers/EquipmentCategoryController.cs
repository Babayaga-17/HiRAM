using ASI.Basecode.Services.Exceptions;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using ASI.Basecode.WebApp.Authentication;
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
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;

namespace ASI.Basecode.WebApp.Controllers;

[Authorize]
public class EquipmentCategoryController : ControllerBase<EquipmentCategoryController>
{
    private readonly IEquipmentCategoryService _equipmentCategoryService;

    public EquipmentCategoryController(
                            IHttpContextAccessor httpContextAccessor,
                            ILoggerFactory loggerFactory,
                            IConfiguration configuration,
                            IEquipmentCategoryService equipmentCategoryService,
                            IMapper mapper = null) : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        this._equipmentCategoryService = equipmentCategoryService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        try
        {
            var equipmentCategories = _equipmentCategoryService.GetEquipmentCategories();
            return View(equipmentCategories);
        }
        catch (Exception)
        {
            TempData["CategoryLoadError"] = "Unable to load equipment categories. Please try again.";

            return View(new List<EquipmentCategoryListItem>());
        }
    }

    [HttpGet]
    public IActionResult Create() => View(new EquipmentCategoryFormViewModel());

    [HttpPost]
    //[ValidateAntiForgeryToken]
    public IActionResult Create(EquipmentCategoryFormViewModel model)
    {
        try
        {
            _equipmentCategoryService.AddEquipmentCategory(model, UserId);

            return RedirectToAction(nameof(Index));
        }
        catch (EquipmentCategoryValidationException exception)
        {
            foreach (var error in exception.Errors)
                ModelState.AddModelError(error.Key, error.Value);

            return View(model);
        }
        catch (Exception)
        {
            ModelState.AddModelError(string.Empty, "Unable to save the category. Please try again.");

            return View(model);
        }
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        try
        {
            var equipmentCategory = _equipmentCategoryService.GetEquipmentCategoryById(id);

            if (equipmentCategory == null)
                return NotFound();

            return View(equipmentCategory);
        }
        catch (Exception exception)
        {
            return StatusCode(500, "Unable to load the category. Please try again.");
        }
    }

    [HttpPost]
    //[ValidateAntiForgeryToken]
    public IActionResult Edit(EquipmentCategoryFormViewModel model)
    {
        EquipmentCategoryListItem equipmentCategoryListItem = null;

        try
        {
            equipmentCategoryListItem = _equipmentCategoryService.GetEquipmentCategoryById(model.CategoryId);

            if (equipmentCategoryListItem == null)
                return NotFound();

            var updatedEquipmentCategory = _equipmentCategoryService.EditEquipmentCategory(model, UserId);

            if (!updatedEquipmentCategory)
                return NotFound();

            return RedirectToAction(nameof(Index));
        }
        catch (EquipmentCategoryValidationException exception)
        {
            foreach (var error in exception.Errors)
                ModelState.AddModelError(error.Key, error.Value);

            return View(equipmentCategoryListItem);
        }
        catch (Exception)
        {
            if (equipmentCategoryListItem == null)
            {
                return StatusCode(500, "Unable to load the category. Please try again.");
            }

            ModelState.AddModelError(string.Empty, "Unable to save the changes. Please try again.");

            return View(equipmentCategoryListItem);
        }
    }

    [HttpPost]
    //[ValidateAntiForgeryToken]
    public IActionResult Deactivate(int id) => ChangeCategoryStatus(id, false);

    [HttpPost]
    //[ValidateAntiForgeryToken]
    public IActionResult Activate(int id) => ChangeCategoryStatus(id, true);

    [HttpPost]
    //[ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        try
        {
            var isDeleted = _equipmentCategoryService.DeleteEquipmentCategory(id);
            if (isDeleted)
            {
                TempData["CategorySuccess"] = "The category was deleted.";
            }
            else
            {
                TempData["CategoryError"] = "The category no longer exists.";
            }
        }
        catch (EquipmentCategoryValidationException exception)
        {
            TempData["CategoryError"] = string.Join(" ", exception.Errors.Values);
        }
        catch (Exception exception)
        {
            TempData["CategoryError"] = "Unable to delete the category. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    private IActionResult ChangeCategoryStatus(int id, bool isActive)
    {
        try
        {
            if (_equipmentCategoryService.SetEquipmentCategoryStatus(id, isActive, UserId))
            {
                TempData["CategorySuccess"] = isActive ? "The category was activated." : "The category was deactivated.";
            }
            else
            {
                TempData["CategoryError"] = "The category no longer exists.";
            }
        }
        catch (Exception)
        {
            TempData["CategoryError"] = "Unable to change the category status. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }
}
