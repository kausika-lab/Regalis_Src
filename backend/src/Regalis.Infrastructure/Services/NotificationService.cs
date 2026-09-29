using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Operations;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services;

public class NotificationService(
    RegalisDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : INotificationService
{
    public async Task<PagedResult<NotificationDto>> GetMyAsync(NotificationListRequest request,
        CancellationToken ct = default)
    {
        var query = db.Notifications.AsNoTracking()
            .Where(n => n.UserId == currentUser.UserId);
        if (request.Category is not null)
        {
            query = query.Where(n => (byte)n.Category == request.Category);
        }
        if (request.IsRead is not null)
        {
            query = query.Where(n => n.IsRead == request.IsRead);
        }
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.NotificationId)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => ToDto(n))
            .ToListAsync(ct);
        return PagedResult<NotificationDto>.Create(items, request.Page, request.PageSize, totalCount);
    }

    public Task<int> GetMyUnreadCountAsync(CancellationToken ct = default) =>
        db.Notifications.CountAsync(n => n.UserId == currentUser.UserId && !n.IsRead, ct);

    public async Task MarkReadAsync(long notificationId, CancellationToken ct = default)
    {
        var notification = await LoadOwnAsync(notificationId, ct);
        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadUtc = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task MarkAllReadAsync(CancellationToken ct = default)
    {
        var nowUtc = clock.UtcNow;
        var unread = await db.Notifications
            .Where(n => n.UserId == currentUser.UserId && !n.IsRead)
            .ToListAsync(ct);
        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadUtc = nowUtc;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long notificationId, CancellationToken ct = default)
    {
        var notification = await LoadOwnAsync(notificationId, ct);
        db.Notifications.Remove(notification); // interceptor converts to soft delete
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> BroadcastAsync(BroadcastNotificationRequest request,
        CancellationToken ct = default)
    {
        var audience = (NotificationAudience)request.Audience;
        var recipientsQuery = audience == NotificationAudience.AllCustomers
            ? db.Users.Where(u => u.UserType == UserType.Customer && u.Status == UserStatus.Active)
            : db.Users.Where(u => u.UserType != UserType.Customer && u.Status == UserStatus.Active);
        var recipientIds = await recipientsQuery.Select(u => u.UserId).ToListAsync(ct);

        var nowUtc = clock.UtcNow;
        foreach (var userId in recipientIds)
        {
            db.Notifications.Add(new Notification
            {
                UserId = userId,
                Audience = audience,
                Title = request.Title.Trim(),
                Message = request.Message.Trim(),
                Category = (NotificationCategory)request.Category,
                Priority = NotificationPriority.Normal,
                ActionLabel = request.ActionLabel?.Trim(),
                ActionRoute = request.ActionRoute?.Trim(),
                SentUtc = nowUtc,
                CreatedByUserId = currentUser.UserId
            });
        }
        await db.SaveChangesAsync(ct);
        return recipientIds.Count;
    }

    private async Task<Notification> LoadOwnAsync(long notificationId, CancellationToken ct)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId, ct)
            ?? throw new NotFoundException("Notification", notificationId);
        if (notification.UserId != currentUser.UserId)
        {
            throw new NotFoundException("Notification", notificationId);
        }
        return notification;
    }

    private static NotificationDto ToDto(Notification n) => new(
        n.NotificationId, n.Title, n.Message, (byte)n.Category, (byte)n.Priority,
        n.ActionLabel, n.ActionRoute, n.IsRead, n.ReadUtc, n.CreatedDate,
        n.ReferenceType, n.ReferenceId);
}
