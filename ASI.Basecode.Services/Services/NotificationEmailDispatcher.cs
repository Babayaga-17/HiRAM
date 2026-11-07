using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services;

public class NotificationEmailDispatcher : INotificationEmailDispatcher
{
    private readonly INotificationRepository _repository;
    private readonly IEmailManager _email;
    private readonly ILogger<NotificationEmailDispatcher> _logger;
    public NotificationEmailDispatcher(INotificationRepository repository, IEmailManager email, ILogger<NotificationEmailDispatcher> logger)
    { _repository = repository; _email = email; _logger = logger; }

    public async Task<bool> DispatchNextAsync(CancellationToken token)
    {
        const int attempts = 5;
        var now = DateTime.UtcNow;
        var lease = Guid.NewGuid();
        var message = await _repository.ClaimEmailAsync(now, now.AddMinutes(2), lease, attempts, token);
        if (message == null) return false;
        try
        {
            await _email.SendAsync(message.RecipientEmail, message.Subject, message.Body, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Notification email attempt failed for delivery {DeliveryId}.", message.NotificationEmailId);
            var failed = message.AttemptCount >= attempts;
            await _repository.FailEmailAsync(message.NotificationEmailId, lease,
                failed ? EmailDeliveryStatuses.Failed : EmailDeliveryStatuses.Pending,
                DateTime.UtcNow.AddMinutes(Math.Min(60, Math.Pow(3, message.AttemptCount))),
                failed ? "Email delivery failed after the maximum attempts." : "Email delivery failed; another attempt is scheduled.", token);
            return true;
        }
        // Persist provider acceptance separately. A database failure here must not trigger immediate SMTP resend.
        if (!await _repository.CompleteEmailAsync(message.NotificationEmailId, lease, DateTime.UtcNow, token))
            _logger.LogWarning("Notification delivery {DeliveryId} was accepted but its lease is no longer owned.", message.NotificationEmailId);
        return true;
    }
}
