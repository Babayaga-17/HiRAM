using ASI.Basecode.Resources.Constants;
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
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers;

[Authorize(Roles = "Custodian, Admin")]
public class EquipmentController : ControllerBase<EquipmentController>
{
    private readonly IEquipmentService _equipmentService;
    private readonly IEquipmentCategoryService _equipmentCategoryService;
    private readonly IEquipmentPhotoService _equipmentPhotoService;

    public EquipmentController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IEquipmentService equipmentService,
        IEquipmentCategoryService equipmentCategoryService,
        IEquipmentPhotoService equipmentPhotoService,
        IMapper mapper = null) : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _equipmentService = equipmentService;
        _equipmentCategoryService = equipmentCategoryService;
        _equipmentPhotoService = equipmentPhotoService;
    }

    [HttpGet]
    public IActionResult Index(EquipmentListRequest request)
    {
        try
        {
            LoadCategories(null, true);
            var model = _equipmentService.GetEquipmentPage(request);
            return View(model);
        }
        catch (Exception)
        {
            TempData["EquipmentLoadError"] = "Unable to load equipment. Please try again.";
            return View(new EquipmentIndexViewModel());
        }
    }

    [HttpGet]
    public IActionResult Create()
    {
        LoadCategories(null);
        return View(new EquipmentFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EquipmentFormViewModel equipment, List<IFormFile> photos)
    {
        //const long maxPhotoBytes = 5 * 1024 * 1024;

        photos ??= new List<IFormFile>();
        var uploads = new List<(IFormFile File, string Path, string ContentType)>();

        if (photos.Count > EquipmentPhotoPolicy.MaxPhotosPerEquipment)
        {
            ModelState.AddModelError("photos", $"Choose up to {EquipmentPhotoPolicy.MaxPhotosPerEquipment} photos.");
        }
        else
        {
            foreach (var photo in photos)
            {
                if (photo.Length == 0 || photo.Length > EquipmentPhotoPolicy.MaxPhotoBytes)
                {
                    ModelState.AddModelError(
                        "photos", $"Each photo must be nonempty and at most {EquipmentPhotoPolicy.MaxPhotoSizeMb} MB.");
                    break;
                }

                var format = GetPhotoFormat(photo);

                if (format == null)
                {
                    ModelState.AddModelError(
                        "photos", "Choose JPG, PNG, or WebP images.");
                    break;
                }

                uploads.Add((photo, $"equipment/{Guid.NewGuid():N}{format.Value.Extension}", format.Value.ContentType));
            }
        }

        if (ModelState.IsValid)
        {
            var attemptedPaths = new List<string>();
            var savingEquipment = false;

            try
            {
                foreach (var upload in uploads)
                {
                    attemptedPaths.Add(upload.Path);

                    using var stream = upload.File.OpenReadStream();
                    await _equipmentPhotoService.UploadAsync(upload.Path, stream, upload.ContentType, HttpContext.RequestAborted);
                }

                savingEquipment = true;
                _equipmentService.AddEquipment(equipment, UserId, attemptedPaths);

                TempData["EquipmentSuccess"] = "The equipment was added.";
                return RedirectToAction(nameof(Index));
            }
            catch (EquipmentValidationException exception)
            {
                await CleanUpPhotosAsync(attemptedPaths);

                foreach (var error in exception.Errors)
                    ModelState.AddModelError(error.Key, error.Value);
            }
            catch (Exception exception)
            {
                if (!savingEquipment)
                    await CleanUpPhotosAsync(attemptedPaths);

                //_logger.LogError(exception, "Unable to save equipment. Attempted photo paths: {PhotoPaths}", string.Join(", ", attemptedPaths));

                ModelState.AddModelError(string.Empty, "Unable to save the equipment. Please try again.");
            }
        }

        LoadCategories(equipment.CategoryId);
        return View(equipment);
    }

    private async Task CleanUpPhotosAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                await _equipmentPhotoService.DeleteAsync(path);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Unable to clean up equipment photo {PhotoPath}.", path);
            }
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var equipment = _equipmentService.GetEquipmentById(id);

            if (equipment == null)
                return NotFound();

            LoadCategories(equipment.CategoryId);
            var model = new EquipmentFormViewModel(equipment);
            await LoadSavedPhotosAsync(model);
            return View(model);
        }
        catch (Exception)
        {
            TempData["EquipmentError"] = "Unable to load the equipment. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    public async Task<IActionResult> Edit(EquipmentFormViewModel equipment)
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
        await LoadSavedPhotosAsync(equipment);
        return View(equipment);
    }

    [HttpPost]
    public IActionResult Activate(int id, EquipmentListRequest request)
    {
        return SetStatus(id, true, request);
    }

    [HttpPost]
    public IActionResult Deactivate(int id, EquipmentListRequest request)
    {
        return SetStatus(id, false, request);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id, EquipmentListRequest request)
    {
        try
        {
            var equipment = _equipmentService.GetEquipmentById(id);

            if (equipment == null)
            {
                TempData["EquipmentError"] = "The equipment no longer exists.";
                return RedirectToList(request);
            }

            if (equipment.IsActive)
            {
                TempData["EquipmentError"] =
                    "Please deactivate the equipment before deleting it.";

                return RedirectToList(request);
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
            TempData["EquipmentError"] = string.Join(" ", exception.Errors.Values);
        }
        catch (Exception)
        {
            // _logger.LogError(exception, "Unable to delete equipment.");

            TempData["EquipmentError"] =
                "Unable to delete the equipment. Please try again.";
        }

        return RedirectToList(request);
    }

    private IActionResult SetStatus(int id, bool isActive, EquipmentListRequest request)
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

        return RedirectToList(request);
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

    private IActionResult RedirectToList(EquipmentListRequest request)
    {
        return RedirectToAction(nameof(Index), new
        {
            page = request.Page,
            search = request.Search,
            categoryId = request.CategoryId,
            status = request.Status
        });
    }

    /* 
     * Checks the file signature and returns the extension and MIME type used for the upload. 
     * 
     * This catches files renamed to look like images. It checks the header, so a corrupt image 
     * with a valid header can still pass.
     */
    private static (string Extension, string ContentType)? GetPhotoFormat(IFormFile photo)
    {
        using var stream = photo.OpenReadStream();
        using var reader = new BinaryReader(stream);
        var bytes = reader.ReadBytes(12);

        if (bytes.Length >= 3
            && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return (".jpg", "image/jpeg");
        }

        if (bytes.AsSpan().StartsWith(
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return (".png", "image/png");
        }

        if (bytes.Length == 12
            && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8))
        {
            return (".webp", "image/webp");
        }

        return null;
    }

    private async Task LoadSavedPhotosAsync(EquipmentFormViewModel model)
    {
        try
        {
            foreach (var photo in _equipmentService.GetEquipmentPhotos(model.EquipmentId))
            {
                var url = await _equipmentPhotoService.CreateReadUrlAsync(
                    photo.StoragePath, HttpContext.RequestAborted);

                model.SavedPhotoUrls.Add(url);
            }
        }
        catch (Exception exception) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            model.SavedPhotoUrls.Clear();
            model.SavedPhotosUnavailable = true;

            _logger.LogWarning(
                exception,
                "Unable to load photos for equipment {EquipmentId}.",
                model.EquipmentId);
        }
    }
}