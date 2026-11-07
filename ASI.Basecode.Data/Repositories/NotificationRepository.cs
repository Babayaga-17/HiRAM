using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Repositories;

public class NotificationRepository : BaseRepository, INotificationRepository
{
    public NotificationRepository(IUnitOfWork unit) : base(unit) { }

    public List<(int UserId, string Email)> GetRecipients(string role, int? userId, bool activeOnly)
    {
        var query = GetDbSet<Account>().AsNoTracking();
        if (userId.HasValue) query = query.Where(a => a.UserId == userId.Value);
        if (role != null) query = query.Where(a => a.Role.RoleName == role);
        if (activeOnly) query = query.Where(a => a.AccountStatus == AccountStatuses.Active && a.Role.IsActive);
        return query.Select(a => new { a.UserId, a.Email }).ToList().Select(a => (a.UserId, a.Email)).ToList();
    }

    // The owning business repository performs SaveChanges, including these staged graphs.
    public void Stage(IEnumerable<Notification> notifications) => GetDbSet<Notification>().AddRange(notifications);

    private IQueryable<Notification> Query(int userId, string search, bool? read)
    {
        var query = GetDbSet<Notification>().AsNoTracking().Where(n => n.RecipientUserId == userId);
        if (!string.IsNullOrEmpty(search)) query = query.Where(n => n.Title.Contains(search) || n.Body.Contains(search));
        if (read.HasValue) query = read.Value ? query.Where(n => n.ReadAt != null) : query.Where(n => n.ReadAt == null);
        return query;
    }
    public Task<int> CountAsync(int userId, string search, bool? read, CancellationToken token) => Query(userId, search, read).CountAsync(token);
    public Task<List<Notification>> GetPageAsync(int userId, string search, bool? read, int skip, int take, CancellationToken token) =>
        Query(userId, search, read).OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.NotificationId)
            .Skip(skip).Take(take).ToListAsync(token);
    public Task<Notification> GetAsync(long id, int userId, CancellationToken token) =>
        Query(userId, null, null).SingleOrDefaultAsync(n => n.NotificationId == id, token);

    public async Task<bool> MarkReadAsync(long id, int userId, DateTime now, CancellationToken token)
    {
        await GetDbSet<Notification>().Where(n => n.NotificationId == id && n.RecipientUserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.ReadAt, (DateTime?)now), token);
        return await Query(userId, null, null).AnyAsync(n => n.NotificationId == id, token);
    }

    public async Task<NotificationEmail> ClaimEmailAsync(DateTime now, DateTime leaseUntil, Guid leaseToken, int maxAttempts, CancellationToken token)
    {
        // Pooled SQL connections can retain a previous serializable isolation level.
        // READPAST requires an explicit read-committed claim transaction.
        await using var transaction = await Context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        // Recover a worker that stopped during its final attempt instead of leaving a permanent lease.
        await GetDbSet<NotificationEmail>().Where(e => e.DeliveryStatus == EmailDeliveryStatuses.Processing
                && e.LockedUntil <= now && e.AttemptCount >= maxAttempts)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.DeliveryStatus, EmailDeliveryStatuses.Failed)
                .SetProperty(e => e.LeaseToken, (Guid?)null).SetProperty(e => e.LockedUntil, (DateTime?)null)
                .SetProperty(e => e.LastError, "Delivery attempts exhausted after an interrupted attempt."), token);

        // One atomic SQL statement claims a single message; SMTP runs after this statement completes.
        var rows = await GetDbSet<NotificationEmail>().FromSqlInterpolated($@"
            ;WITH candidate AS (
                SELECT TOP (1) * FROM dbo.NotificationEmails WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK)
                WHERE attempt_count < {maxAttempts} AND
                    ((delivery_status = 'Pending' AND next_attempt_at <= {now})
                     OR (delivery_status = 'Processing' AND locked_until <= {now}))
                ORDER BY next_attempt_at, notification_email_id
            )
            UPDATE candidate SET delivery_status = 'Processing', attempt_count = attempt_count + 1,
                lease_token = {leaseToken}, locked_until = {leaseUntil}
            OUTPUT inserted.*;").AsNoTracking().ToListAsync(token);
        await transaction.CommitAsync(token);
        return rows.FirstOrDefault();
    }

    public async Task<bool> CompleteEmailAsync(long id, Guid leaseToken, DateTime now, CancellationToken token) =>
        await GetDbSet<NotificationEmail>().Where(e => e.NotificationEmailId == id && e.LeaseToken == leaseToken && e.DeliveryStatus == EmailDeliveryStatuses.Processing)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.DeliveryStatus, EmailDeliveryStatuses.Sent)
                .SetProperty(e => e.SentAt, (DateTime?)now).SetProperty(e => e.LeaseToken, (Guid?)null)
                .SetProperty(e => e.LockedUntil, (DateTime?)null).SetProperty(e => e.LastError, (string)null), token) == 1;

    public async Task<bool> FailEmailAsync(long id, Guid leaseToken, string status, DateTime nextAttempt, string error, CancellationToken token) =>
        await GetDbSet<NotificationEmail>().Where(e => e.NotificationEmailId == id && e.LeaseToken == leaseToken && e.DeliveryStatus == EmailDeliveryStatuses.Processing)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.DeliveryStatus, status).SetProperty(e => e.NextAttemptAt, nextAttempt)
                .SetProperty(e => e.LeaseToken, (Guid?)null).SetProperty(e => e.LockedUntil, (DateTime?)null)
                .SetProperty(e => e.LastError, error), token) == 1;
}
