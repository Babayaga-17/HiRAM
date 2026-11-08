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

[Authorize(Roles = $"{RoleNames.Borrower}, {RoleNames.Custodian}, {RoleNames.Admin}")]
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
    [Authorize(Roles = "Custodian,Admin")]
    public async Task<IActionResult> Index(EquipmentListRequest request)
    {
        try
        {
            LoadCategories(null, true);
            var model = _equipmentService.GetEquipmentPage(request);
            var cancellationToken = HttpContext.RequestAborted;

            foreach (var item in model.Items)
            {
                if (string.IsNullOrWhiteSpace(item.CoverPhotoStoragePath))
                    continue;

                try
                {
                    var coverUrl = await _equipmentPhotoService.CreateReadUrlAsync(
                        item.CoverPhotoStoragePath, cancellationToken);
                    item.ImageUrls.Add(coverUrl);
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        exception, "Unable to load the cover for equipment {EquipmentId}.", item.EquipmentId);
                }
            }

            return View(model);
        }
        catch (Exception) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            TempData["EquipmentLoadError"] = "Unable to load equipment. Please try again.";
            return View(new EquipmentIndexViewModel());
        }
    }

    [HttpGet]
    [Authorize(Roles = "Custodian,Admin")]
    public IActionResult Schedule(int id, DateTime? from, DateTime? to, int? reservationId)
    {
        if (_equipmentService.GetEquipmentById(id) == null)
            return NotFound();
        if (!from.HasValue || !to.HasValue || to <= from || (to.Value - from.Value).TotalDays > 62)
            return BadRequest(new { message = "Choose a calendar range of up to 62 days." });

        try
        {
            if (_equipmentService.GetEquipmentById(id) == null)
                return NotFound();

            var periods = _equipmentService.GetSchedule(id, from.Value, to.Value);
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
    [Authorize(Roles = "Custodian,Admin")]
    public IActionResult Availability(int id, DateTime? from, DateTime? to, int? reservationId)
    {
        if (_equipmentService.GetEquipmentById(id) == null)
            return NotFound();

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
                    start = period.Start.ToString("MMM d, yyyy h:mm tt"),
                    end = period.End.ToString("MMM d, yyyy h:mm tt")
                }),
                slots = result.Slots.Select(slot => new
                {
                    start = slot.Start.ToString("MMM d, yyyy h:mm tt"),
                    end = slot.End.ToString("MMM d, yyyy h:mm tt")
                })
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load equipment availability.");
            return StatusCode(500, new { message = "Unable to load availability." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, int? reservationId)
    {
        var isBorrower = User.IsInRole("Borrower");

        // Reservation editing remains on the borrower route until
        // its ownership checks are migrated.
        if (isBorrower && reservationId.HasValue)
            return RedirectToAction(
                "Details",
                "BrowseEquipment",
                new { id, reservationId });

        var equipment = isBorrower
            ? _equipmentService.GetBorrowableEquipmentById(id)
            : _equipmentService.GetEquipmentById(id);

        if (equipment == null)
            return NotFound();

        var cancellationToken = HttpContext.RequestAborted;

        foreach (var photo in _equipmentService.GetEquipmentPhotos(id))
        {
            try
            {
                equipment.ImageUrls.Add(
                    await _equipmentPhotoService.CreateReadUrlAsync(
                        photo.StoragePath, cancellationToken));
            }
            catch (Exception exception)
                when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    exception,
                    "Unable to load photo {PhotoId} for equipment {EquipmentId}.",
                    photo.PhotoId, id);
            }
        }

        return View(equipment);
    }

    [HttpGet]
    [Authorize(Roles = "Custodian,Admin")]
    public IActionResult Create()
    {
        LoadCategories(null);
        return View(new EquipmentFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Custodian,Admin")]
    public async Task<IActionResult> Create(EquipmentFormViewModel equipment, List<IFormFile> photos)
    {
        var uploads = PreparePhotoUploads(photos, EquipmentPhotoPolicy.MaxPhotosPerEquipment);

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
            catch (Exception)
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

    private List<(IFormFile File, string Path, string ContentType)> PreparePhotoUploads(List<IFormFile> photos, int availableSlots)
    {
        var uploads = new List<(IFormFile File, string Path, string ContentType)>();

        if (photos == null || photos.Count == 0)
            return uploads;

        if (photos.Count > availableSlots)
        {
            var message = availableSlots == 0
                ? "This equipment already has the maximum number of photos."
                : $"Choose up to {availableSlots} photos.";

            ModelState.AddModelError(nameof(photos), message);
            return uploads;
        }

        foreach (var photo in photos)
        {
            if (photo.Length == 0 || photo.Length > EquipmentPhotoPolicy.MaxPhotoBytes)
            {
                ModelState.AddModelError(
                    nameof(photos),
                    $"Each photo must be nonempty and at most {EquipmentPhotoPolicy.MaxPhotoSizeMb} MB.");
                break;
            }

            var format = GetPhotoFormat(photo);

            if (format == null)
            {
                ModelState.AddModelError(
                    nameof(photos), "Choose JPG, PNG, or WebP images.");
                break;
            }

            uploads.Add((photo, $"equipment/{Guid.NewGuid():N}{format.Value.Extension}", format.Value.ContentType));
        }

        return uploads;
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
    [Authorize(Roles = "Custodian, Admin")]
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
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Custodian, Admin")]
    public async Task<IActionResult> Edit(EquipmentFormViewModel equipment, List<IFormFile> photos, List<int> removedPhotoIds)
    {
        var attemptedPaths = new List<string>();
        var savingEquipment = false;

        try
        {
            if (ModelState.IsValid && photos is { Count: > 0 })
            {
                var availableSlots = EquipmentPhotoPolicy.MaxPhotosPerEquipment;

                if (removedPhotoIds is not { Count: > 0 })
                {
                    var savedCount = _equipmentService.GetEquipmentPhotos(equipment.EquipmentId).Count;
                    availableSlots = Math.Max(0, availableSlots - savedCount);
                }

                var uploads = PreparePhotoUploads(photos, availableSlots);

                if (ModelState.IsValid)
                {
                    foreach (var upload in uploads)
                    {
                        attemptedPaths.Add(upload.Path);

                        using var stream = upload.File.OpenReadStream();
                        await _equipmentPhotoService.UploadAsync(
                            upload.Path, stream, upload.ContentType, HttpContext.RequestAborted);
                    }
                }
            }

            if (ModelState.IsValid)
            {
                savingEquipment = true;

                var result = _equipmentService.EditEquipment(equipment, UserId, attemptedPaths, removedPhotoIds);

                if (!result.Found)
                {
                    await CleanUpPhotosAsync(attemptedPaths);
                    return NotFound();
                }

                await CleanUpPhotosAsync(result.RemovedPhotoPaths);

                TempData["EquipmentSuccess"] = "The equipment was updated.";
                return RedirectToAction(nameof(Index));
            }
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

            _logger.LogError(exception, "Unable to save equipment changes.");
            ModelState.AddModelError(string.Empty, "Unable to save the changes. Please try again.");
        }

        LoadCategories(equipment.CategoryId);
        await LoadSavedPhotosAsync(equipment);
        return View(equipment);
    }

    [HttpPost]
    [Authorize(Roles = "Custodian, Admin")]
    public IActionResult Activate(int id, EquipmentListRequest request, bool returnToDetails = false)
    {
        return SetStatus(id, true, request, returnToDetails);
    }

    [HttpPost]
    [Authorize(Roles = "Custodian, Admin")]
    public IActionResult Deactivate(int id, EquipmentListRequest request, bool returnToDetails = false)
    {
        return SetStatus(id, false, request, returnToDetails);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Custodian, Admin")]
    public async Task<IActionResult> Delete(int id, EquipmentListRequest request)
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

            var removedPhotoPaths = _equipmentService.DeleteEquipment(id, UserId);
            await CleanUpPhotosAsync(removedPhotoPaths);

            TempData["EquipmentSuccess"] =
                "The equipment was deleted successfully.";
        }
        catch (EquipmentValidationException exception)
        {
            TempData["EquipmentError"] = string.Join(" ", exception.Errors.Values);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to delete equipment {EquipmentId}.", id);

            TempData["EquipmentError"] =
                "Unable to delete the equipment. Please try again.";
        }

        return RedirectToList(request);
    }

    private IActionResult SetStatus(int id, bool isActive, EquipmentListRequest request, bool returnToDetails)
    {
        try
        {
            bool setToNewStatus = _equipmentService.SetEquipmentStatus(id, isActive, UserId);
            if (setToNewStatus)
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

        return returnToDetails && _equipmentService.GetEquipmentById(id) != null
            ? RedirectToAction(nameof(Details), new { id })
            : RedirectToList(request);
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

    private async Task LoadSavedPhotosAsync(EquipmentFormViewModel model)
    {
        try
        {
            foreach (var photo in _equipmentService.GetEquipmentPhotos(model.EquipmentId))
            {
                var url = await _equipmentPhotoService.CreateReadUrlAsync(
                    photo.StoragePath, HttpContext.RequestAborted);

                model.SavedPhotos.Add(new EquipmentPhotoPreview
                {
                    PhotoId = photo.PhotoId,
                    Url = url
                });
            }
        }
        catch (Exception exception) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            model.SavedPhotos.Clear();
            model.SavedPhotosUnavailable = true;

            _logger.LogWarning(
                exception,
                "Unable to load photos for equipment {EquipmentId}.",
                model.EquipmentId);
        }
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
}

