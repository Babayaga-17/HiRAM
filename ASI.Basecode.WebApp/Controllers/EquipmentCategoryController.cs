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
            var categories = _equipmentCategoryService
                .GetEquipmentCategories()
                .Select(category => new EquipmentCategoryViewModel
                {
                    CategoryId = category.CategoryId,
                    CategoryCode = category.CategoryCode,
                    CategoryName = category.CategoryName,
                    Description = category.Description,
                    EquipmentCount = category.EquipmentCount,
                    IsActive = category.IsActive
                })
                .ToList();

            return View(categories);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to load equipment categories.");

            TempData["CategoryLoadError"] =
                "Unable to load equipment categories. Please try again.";

            return View(Array.Empty<EquipmentCategoryViewModel>());
        }
    }

    [HttpGet]
    public IActionResult Create() => View(new EquipmentCategoryFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(EquipmentCategoryFormViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var createdBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(createdBy))
            return Unauthorized();

        try
        {
            _equipmentCategoryService.AddEquipmentCategory(
                model.CategoryCode,
                model.CategoryName,
                model.Description,
                model.IsActive,
                createdBy);

            return RedirectToAction(nameof(Index));
        }
        catch (EquipmentCategoryValidationException exception)
        {
            foreach (var error in exception.Errors)
                ModelState.AddModelError(error.Key, error.Value);

            return View(model);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to create an equipment category for user {UserId}.",
                createdBy);

            ModelState.AddModelError(
                string.Empty,
                "Unable to save the category. Please try again.");

            return View(model);
        }
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        if (id <= 0)
            return NotFound();

        try
        {
            var category = _equipmentCategoryService.GetEquipmentCategoryById(id);

            if (category == null) 
                return NotFound();

            SetEditPageData(category);

            return View(new EquipmentCategoryFormViewModel
            {
                CategoryCode = category.CategoryCode,
                CategoryName = category.CategoryName,
                Description = category.Description,
                IsActive = category.IsActive
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load category {CategoryId}.", id);

            return StatusCode(500, "Unable to load the category. Please try again.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, EquipmentCategoryFormViewModel model)
    {
        if (id <= 0)
            return NotFound();

        var updatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(updatedBy))
            return Unauthorized();

        EquipmentCategoryListItem category = null;

        try
        {
            category = _equipmentCategoryService.GetEquipmentCategoryById(id);

            if (category == null)
                return NotFound();

            SetEditPageData(category);

            if (!ModelState.IsValid)
                return View(model);

            var updated = _equipmentCategoryService.EditEquipmentCategory(
                id,
                model.CategoryCode,
                model.CategoryName,
                model.Description,
                model.IsActive,
                updatedBy
            );

            if (!updated)
                return NotFound();

            return RedirectToAction(nameof(Index));
        }
        catch (EquipmentCategoryValidationException exception)
        {
            foreach (var error in exception.Errors)
                ModelState.AddModelError(error.Key, error.Value);

            return View(model);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception, "Failed to edit category {CategoryId}.", id);

            if (category == null)
            {
                return StatusCode(
                    500, "Unable to load the category. Please try again.");
            }

            ModelState.AddModelError(
                string.Empty,
                "Unable to save the changes. Please try again.");

            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Deactivate(int id) => ChangeCategoryStatus(id, false);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Activate(int id) => ChangeCategoryStatus(id, true);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        if (id <= 0)
            return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        try
        {
            if (_equipmentCategoryService.DeleteEquipmentCategory(id))
            {
                _logger.LogInformation("Deleted equipment category {CategoryId} by user {UserId}.", id, userId);
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
            _logger.LogError(exception, "Failed to delete category {CategoryId} for user {UserId}.", id, userId);
            TempData["CategoryError"] = "Unable to delete the category. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    private IActionResult ChangeCategoryStatus(int id, bool isActive)
    {
        if (id <= 0)
            return NotFound();

        var updatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(updatedBy))
            return Unauthorized();

        try
        {
            if (_equipmentCategoryService.SetEquipmentCategoryStatus(id, isActive, updatedBy))
            {
                TempData["CategorySuccess"] = isActive
                    ? "The category was activated."
                    : "The category was deactivated.";
            }
            else
            {
                TempData["CategoryError"] = "The category no longer exists.";
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Failed to set category {CategoryId} active status to {IsActive} for user {UserId}.",
                id, isActive, updatedBy);
            TempData["CategoryError"] = "Unable to change the category status. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    private void SetEditPageData(EquipmentCategoryListItem category)
    {
        ViewData["CategoryId"] = category.CategoryId;
        ViewData["EquipmentCount"] = category.EquipmentCount;
    }
}
