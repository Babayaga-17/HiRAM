using ASI.Basecode.Services.Exceptions;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
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

            ViewData["LoadError"] =
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

    public IActionResult Edit(int id)
    {
        // No category can be edited until the list is connected to stored data.
        return NotFound();
    }
}
