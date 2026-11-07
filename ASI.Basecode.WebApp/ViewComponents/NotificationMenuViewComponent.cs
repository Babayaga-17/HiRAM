using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.ViewComponents;

public class NotificationMenuViewComponent : ViewComponent
{
    private readonly INotificationService _notifications;
    private readonly ILogger<NotificationMenuViewComponent> _logger;
    public NotificationMenuViewComponent(INotificationService notifications, ILogger<NotificationMenuViewComponent> logger)
    { _notifications = notifications; _logger = logger; }
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = new NotificationMenuViewModel();
        if (int.TryParse(HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) && id > 0)
        {
            try { model = await _notifications.GetMenuAsync(id, HttpContext.RequestAborted); }
            catch (Exception exception) when (!HttpContext.RequestAborted.IsCancellationRequested)
            { _logger.LogWarning(exception, "Unable to load notification menu for user {UserId}.", id); }
        }
        return View("~/Views/Shared/_NotificationMenu.cshtml", model);
    }
}
