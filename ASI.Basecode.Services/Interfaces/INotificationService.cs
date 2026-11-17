using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ServiceModels;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces;

public interface INotificationService
{
    void RegistrationSubmitted(User user);
    void RegistrationApproved(Account account);
    void ReservationChanged(Reservation reservation, string eventType);
    Task<NotificationListViewModel> GetPageAsync(int userId, string search, string status, int page, CancellationToken token);
    Task<NotificationMenuViewModel> GetMenuAsync(int userId, CancellationToken token);
    Task<NotificationDetailsViewModel> GetDetailsAsync(int userId, long id, CancellationToken token);
    Task<bool> OpenAsync(int userId, long id, CancellationToken token);
}
