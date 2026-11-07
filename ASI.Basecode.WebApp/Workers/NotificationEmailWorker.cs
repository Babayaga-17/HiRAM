using ASI.Basecode.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Workers;

public class NotificationEmailWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<NotificationEmailWorker> _logger;
    public NotificationEmailWorker(IServiceScopeFactory scopes, ILogger<NotificationEmailWorker> logger)
    { _scopes = scopes; _logger = logger; }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                for (var i = 0; i < 20 && !stoppingToken.IsCancellationRequested; i++)
                {
                    using var scope = _scopes.CreateScope();
                    if (!await scope.ServiceProvider.GetRequiredService<INotificationEmailDispatcher>().DispatchNextAsync(stoppingToken)) break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { _logger.LogError(exception, "Notification email dispatch could not complete. It will retry on the next cycle."); }
            try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
