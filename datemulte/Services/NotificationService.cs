using Datemulte_2.Data;
using Datemulte_2.Models;
using Microsoft.EntityFrameworkCore;

namespace Datemulte_2.Services;

public class NotificationService
{
    private readonly IDbContextFactory<DataManagementDbContext> _dbContextFactory;

    public NotificationService(IDbContextFactory<DataManagementDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Notification> CreateNotificationAsync(Guid userId, NotificationType type, string title, string message, string? actionUrl = null, string? contextJson = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            ActionUrl = actionUrl,
            ContextJson = contextJson ?? "{}",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();

        return notification;
    }

    public async Task<List<Notification>> GetUserNotificationsAsync(Guid userId, bool includeRead = true)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var query = dbContext.Notifications.Where(n => n.UserId == userId);

        if (!includeRead)
        {
            query = query.Where(n => !n.IsRead);
        }
        
        return await query.OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync();
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var notification = await dbContext.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if(notification == null)
        {
            return false;
        }

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<int> MarkAllAsReadAsync(Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var unreadNotifications = await dbContext.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync();
        return unreadNotifications.Count;
    }

    public async Task<int> DeleteOldNotificationsAsync(int DaysToKeep = 30)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var cutoffDate = DateTime.UtcNow.AddDays(-DaysToKeep);

        var oldNotifications = await dbContext.Notifications
            .Where(n => n.CreatedAt < cutoffDate)
            .ToListAsync();

        dbContext.Notifications.RemoveRange(oldNotifications);
        await dbContext.SaveChangesAsync();

        return oldNotifications.Count;
    }
}