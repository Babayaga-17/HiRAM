using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers;

[Authorize]
[Authorize(Roles = $"{RoleNames.Borrower}, {RoleNames.Custodian}, {RoleNames.Admin}")]
public class NotificationController : Controller
{
    private readonly INotificationService _notifications;
    private readonly ILogger<NotificationController> _logger;
    public NotificationController(INotificationService notifications, ILogger<NotificationController> logger)
    { _notifications = notifications; _logger = logger; }
    private int? RecipientId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) && id > 0 ? id : null;

    [HttpGet]
    public async Task<IActionResult> Index(string search, string status = "all", int page = 1)
    {
        if (!RecipientId.HasValue) return Challenge();
        try { return View(await _notifications.GetPageAsync(RecipientId.Value, search, status, page, HttpContext.RequestAborted)); }
        catch (Exception exception) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogError(exception, "Unable to load notifications for user {UserId}.", RecipientId);
            return View(new NotificationListViewModel());
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Open(long id, string search, string status = "all", int page = 1)
    {
        if (!RecipientId.HasValue) return Challenge();
        if (!ModelState.IsValid || id < 1) return NotFound();
        try
        {
            if (!await _notifications.OpenAsync(RecipientId.Value, id, HttpContext.RequestAborted)) return NotFound();
            return RedirectToAction(nameof(Details), new { id, search, status, page });
        }
        catch (Exception exception) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogError(exception, "Unable to open notification {NotificationId} for user {UserId}.", id, RecipientId);
            TempData["NotificationError"] = "The notification could not be opened. Please try again.";
            return RedirectToAction(nameof(Index), new { search, status, page });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id, string search, string status = "all", int page = 1)
    {
        if (!RecipientId.HasValue) return Challenge();
        ViewData["NotificationSearch"] = search;
        ViewData["NotificationStatus"] = status;
        ViewData["NotificationPage"] = page;
        try
        {
            var model = await _notifications.GetDetailsAsync(RecipientId.Value, id, HttpContext.RequestAborted);
            if (model == null) return NotFound();
            if (model.EventType == NotificationEvents.RegistrationSubmitted && User.IsInRole(RoleNames.Admin))
            { model.RelatedUrl = Url.Action("Details", "UserAccount", new { id = model.RelatedUserId }); model.RelatedLabel = "Review registration"; }
            else if (model.EventType == NotificationEvents.RegistrationApproved)
            { model.RelatedUrl = Url.Action("MyProfile", "Account"); model.RelatedLabel = "View my profile"; }
            else if (model.ReservationId.HasValue)
            {
                var staff = model.EventType == NotificationEvents.ReservationSubmitted || model.EventType == NotificationEvents.ReservationCancelled;
                if (!staff || User.IsInRole(RoleNames.Custodian) || User.IsInRole(RoleNames.Admin))
                {
                    model.RelatedUrl = Url.Action(
                        staff ? "Review" : "ReservationDetails",
                        staff ? "Reservation" : "EquipmentReservation",
                        new { id = model.ReservationId });

                    model.RelatedLabel = "View reservation"; 
                }
            }
            return View(model);
        }
        catch (Exception exception) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogError(exception, "Unable to load notification {NotificationId} for user {UserId}.", id, RecipientId);
            return View(new NotificationDetailsViewModel());
        }
    }
}
