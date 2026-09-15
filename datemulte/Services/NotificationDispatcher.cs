using Datemulte_2.Data;
using Datemulte_2.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Datemulte_2.Services;

/// <summary>
/// Routes notifications to appropriate channels (in-app, email, etc.)
/// based on user preferences
/// </summary>
public class NotificationDispatcher
{
    private readonly IDbContextFactory<DataManagementDbContext> _dbContextFactory;
    private readonly NotificationService _notificationService;
    private readonly EmailService _emailService;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        IDbContextFactory<DataManagementDbContext> dbContextFactory,
        NotificationService notificationService,
        EmailService emailService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<NotificationDispatcher> logger)
    {
        _dbContextFactory = dbContextFactory;
        _notificationService = notificationService;
        _emailService = emailService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Dispatch a notification to all enabled channels for a user
    /// </summary>
    public async Task DispatchNotificationAsync(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        string? actionUrl = null,
        Dictionary<string, object>? context = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Get user profile first to access email and global settings
        var userProfile = await dbContext.UserProfiles
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (userProfile == null) return;

        // Get user preferences for this specific type
        var preferences = await dbContext.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Type == type);

        // Default to in-app only if no preferences set
        bool sendInApp = preferences?.InAppEnabled ?? true;
        // Send email if:
        // 1. User has an email address
        // 2. Global email notifications are enabled (master switch)
        // 3. Specific type is enabled (or default to false if no preference, assuming opt-in)
        bool sendEmail = !string.IsNullOrEmpty(userProfile.Email) 
                         && userProfile.EmailNotifications 
                         && (preferences?.EmailEnabled ?? false);

        var contextJson = context != null ? JsonSerializer.Serialize(context) : "{}";

        // Always create in-app notification if enabled
        if (sendInApp)
        {
            await _notificationService.CreateNotificationAsync(
                userId, type, title, message, actionUrl, contextJson);
        }

        if (sendEmail && !string.IsNullOrEmpty(userProfile.Email))
        {
            var (success, error) = await SendEmailNotificationAsync(userProfile.Email, type, title, message, actionUrl);
            if (!success)
            {
                _logger.LogWarning("Failed to send notification email to {Email}: {Error}", userProfile.Email, error);
            }
        }
    }

    /// <summary>
    /// Send notification when a session is shared
    /// </summary>
    public async Task NotifySessionSharedAsync(
        Guid sharedWithUserId,
        string sharedByUserName,
        string sessionName,
        Guid sessionId)
    {
        var actionUrl = $"/sessions/{sessionId}";
        var context = new Dictionary<string, object>
        {
            { "sessionId", sessionId },
            { "sharedBy", sharedByUserName }
        };

        await DispatchNotificationAsync(
            sharedWithUserId,
            NotificationType.SessionShared,
            "Session Shared",
            $"{sharedByUserName} shared \"{sessionName}\" with you",
            actionUrl,
            context);
    }

    /// <summary>
    /// Send notification when a comment is added
    /// </summary>
    public async Task NotifyCommentAddedAsync(
        Guid sessionOwnerId,
        string commenterName,
        string sessionName,
        Guid sessionId,
        string commentText)
    {
        var actionUrl = $"/sessions/{sessionId}#comments";
        var context = new Dictionary<string, object>
        {
            { "sessionId", sessionId },
            { "commenter", commenterName }
        };

        await DispatchNotificationAsync(
            sessionOwnerId,
            NotificationType.CommentAdded,
            "New Comment",
            $"{commenterName} commented on \"{sessionName}\"",
            actionUrl,
            context);
    }

    /// <summary>
    /// Send notification when data is changed
    /// </summary>
    public async Task NotifyDataChangedAsync(
        Guid userId,
        string changedByUserName,
        string sessionName,
        Guid sessionId)
    {
        var actionUrl = $"/sessions/{sessionId}";
        var context = new Dictionary<string, object>
        {
            { "sessionId", sessionId },
            { "changedBy", changedByUserName }
        };

        await DispatchNotificationAsync(
            userId,
            NotificationType.DataChanged,
            "Data Updated",
            $"{changedByUserName} updated data in \"{sessionName}\"",
            actionUrl,
            context);
    }

    /// <summary>
    /// Send system alert notification
    /// </summary>
    public async Task NotifySystemAlertAsync(Guid userId, string title, string message)
    {
        await DispatchNotificationAsync(
            userId,
            NotificationType.SystemAlert,
            title,
            message);
    }

    /// <summary>
    /// Helper to send email notifications
    /// </summary>
    private async Task<(bool success, string? error)> SendEmailNotificationAsync(
        string toEmail,
        NotificationType type,
        string title,
        string message,
        string? actionUrl)
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        string baseUrl = request != null 
            ? $"{request.Scheme}://{request.Host}" 
            : "https://datemulte.com"; // Fallback if no request context

        var fullUrl = actionUrl != null ? $"{baseUrl}{actionUrl}" : null;

        var htmlBody = $@"
            <h2>{title}</h2>
            <p>{message}</p>
            {(fullUrl != null ? $@"<p><a href=""{fullUrl}"" style=""background-color: #4CAF50; color: white; padding: 10px 20px; text-decoration: none; border-radius: 4px;"">View Details</a></p>" : "")}
            <p><em>Generated by Datemulte</em></p>
        ";

        return await _emailService.SendEmailAsync(toEmail, title, htmlBody);
    }
}
