using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces;

public interface INotificationRepository
{
    List<(int UserId, string Email)> GetRecipients(string role, int? userId, bool activeOnly);
    void Stage(IEnumerable<Notification> notifications);
    Task<int> CountAsync(int userId, string search, bool? read, CancellationToken token);
    Task<List<Notification>> GetPageAsync(int userId, string search, bool? read, int skip, int take, CancellationToken token);
    Task<Notification> GetAsync(long id, int userId, CancellationToken token);
    Task<bool> MarkReadAsync(long id, int userId, DateTime now, CancellationToken token);
}
