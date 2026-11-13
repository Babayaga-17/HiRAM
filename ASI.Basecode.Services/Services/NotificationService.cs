using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notifications;
    public NotificationService(INotificationRepository notifications) => _notifications = notifications;

    public void RegistrationSubmitted(User user) => Stage(_notifications.GetRecipients(RoleNames.Admin, null, true),
        NotificationEvents.RegistrationSubmitted, "New registration awaiting review",
        $"{user.FirstName} {user.LastName} ({user.UserCode}) registered as a borrower. Review their school details and profile before approving access.", user, null);

    public bool RegistrationApproved(Account account)
    {
        Stage(new[] { (account.UserId, account.Email) }, NotificationEvents.RegistrationApproved, "Your HiRAM registration is approved",
            "Your HiRAM registration has been approved. Your account is active and you are eligible to request equipment. " +
            "You can now sign in with your registered email address and password. Equipment requests remain subject to availability and approval.", account.User, null);
        return !string.IsNullOrWhiteSpace(account.Email);
    }

    public void ReservationChanged(Reservation reservation, string eventType)
    {
        string title, body;
        var staff = eventType == NotificationEvents.ReservationSubmitted || eventType == NotificationEvents.ReservationCancelled;
        switch (eventType)
        {
            case NotificationEvents.ReservationSubmitted:
                title = "New reservation awaiting review";
                body = $"Reservation {reservation.ReservationNumber} was submitted. Review the requested equipment, borrowing schedule and borrower eligibility.";
                break;
            case NotificationEvents.ReservationApproved:
                title = "Your reservation was approved";
                body = $"Reservation {reservation.ReservationNumber} was approved. Contact the equipment office to arrange release. The equipment has not been marked as borrowed yet.";
                break;
            case NotificationEvents.ReservationRejected:
                title = "Your reservation was rejected";
                body = $"Reservation {reservation.ReservationNumber} was rejected.\n\nReason: {reservation.RejectionReason}";
                break;
            case NotificationEvents.ReservationCancelled:
                title = "A reservation was cancelled";
                body = $"Reservation {reservation.ReservationNumber} was cancelled by the borrower.";
                if (!string.IsNullOrWhiteSpace(reservation.CancellationReason)) body += $"\n\nReason: {reservation.CancellationReason}";
                break;
            default: throw new ArgumentException("Unsupported reservation notification event.", nameof(eventType));
        }
        Stage(_notifications.GetRecipients(staff ? RoleNames.Custodian : null, staff ? null : reservation.BorrowerUserId, staff),
            eventType, title, body, null, reservation);
    }

    private void Stage(IEnumerable<(int UserId, string Email)> recipients, string eventType, string title, string body, User user, Reservation reservation)
    {
        var now = DateTime.UtcNow;
        var eventKey = Guid.NewGuid();
        // Keep within the persisted message contract even when old data has unbounded notes.
        body = body.Length > 2000 ? body.Substring(0, 1999) + "…" : body;
        var messages = recipients.DistinctBy(r => r.UserId).Select(recipient => new Notification
        {
            RecipientUserId = recipient.UserId, EventKey = eventKey, EventType = eventType,
            RelatedUser = user, Reservation = reservation, Title = title, Body = body, CreatedAt = now,
            NotificationEmail = string.IsNullOrWhiteSpace(recipient.Email) ? null : new NotificationEmail
            {
                RecipientEmail = recipient.Email, Subject = title, Body = body,
                DeliveryStatus = EmailDeliveryStatuses.Pending, AttemptCount = 0, NextAttemptAt = now, CreatedAt = now
            }
        }).ToList();
        _notifications.Stage(messages);
    }

    public async Task<NotificationListViewModel> GetPageAsync(int userId, string search, string status, int page, CancellationToken token)
    {
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (search?.Length > 200) search = search.Substring(0, 200);
        status = status == "read" || status == "unread" ? status : "all";
        bool? read = status == "all" ? null : status == "read";
        var count = await _notifications.CountAsync(userId, search, read, token);
        const int size = 10;
        var pages = Math.Max(1, (int)Math.Ceiling(count / (double)size));
        page = Math.Clamp(page, 1, pages);
        var unread = await _notifications.CountAsync(userId, null, false, token);
        var rows = await _notifications.GetPageAsync(userId, search, read, (page - 1) * size, size, token);
        return new NotificationListViewModel { IsAvailable = true, Search = search, Status = status, Page = page,
            TotalPages = pages, TotalCount = count, PageSize = size, UnreadCount = unread, Items = rows.Select(Item).ToList() };
    }

    public async Task<NotificationMenuViewModel> GetMenuAsync(int userId, CancellationToken token)
    {
        try
        {
            var unread = await _notifications.CountAsync(userId, null, false, token);
            var rows = await _notifications.GetPageAsync(userId, null, null, 0, 5, token);
            return new NotificationMenuViewModel { IsAvailable = true, UnreadCount = unread, Items = rows.Select(Item).ToList() };
        } 
        catch (Exception exception)
        {
            return null;
        }
        
    }

    public async Task<NotificationDetailsViewModel> GetDetailsAsync(int userId, long id, CancellationToken token)
    {
        var n = await _notifications.GetAsync(id, userId, token);
        return n == null ? null : new NotificationDetailsViewModel
        {
            IsAvailable = true, Id = n.NotificationId, Title = n.Title, Body = n.Body, Category = Category(n.EventType),
            CreatedAt = Local(n.CreatedAt), ReadAt = n.ReadAt.HasValue ? Local(n.ReadAt.Value) : null,
            EventType = n.EventType, RelatedUserId = n.RelatedUserId, ReservationId = n.ReservationId
        };
    }
    public Task<bool> OpenAsync(int userId, long id, CancellationToken token) => _notifications.MarkReadAsync(id, userId, DateTime.UtcNow, token);
    private static string Category(string eventType) => eventType.StartsWith("Registration", StringComparison.Ordinal) ? "Account" : "Reservation";
    private static DateTime Local(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(8)).DateTime;
    private static NotificationItemViewModel Item(Notification n) => new()
    {
        Id = n.NotificationId, Title = n.Title, Preview = n.Body.Length > 110 ? n.Body.Substring(0, 109) + "…" : n.Body,
        Category = Category(n.EventType), IsRead = n.ReadAt.HasValue, CreatedAt = Local(n.CreatedAt)
    };
}
