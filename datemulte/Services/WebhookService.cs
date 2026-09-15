using Datemulte_2.Data;
using Datemulte_2.Models;
using Datemulte_2.Services.Security;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Datemulte_2.Services;

/// <summary>
/// Manages webhook subscriptions
/// </summary>
public class WebhookService
{
    private readonly IDbContextFactory<DataManagementDbContext> _dbContextFactory;
    private readonly AuditLogService _auditLogService;

    public WebhookService(
        IDbContextFactory<DataManagementDbContext> dbContextFactory,
        AuditLogService auditLogService)
    {
        _dbContextFactory = dbContextFactory;
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// Create a new webhook subscription
    /// </summary>
    public async Task<(bool success, Webhook? webhook, string? error)> CreateWebhookAsync(
        Guid userId,
        string url,
        WebhookEventType[] events,
        string? secret = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Validate URL
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            return (false, null, "Invalid URL");

        var webhook = new Webhook
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Url = url,
            Secret = secret,
            EventsJson = JsonSerializer.Serialize(events),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Webhooks.Add(webhook);
        await dbContext.SaveChangesAsync();

        await _auditLogService.LogActionAsync(
            userId,
            AuditAction.Created,
            "Webhook",
            webhook.Id,
            $"Created webhook for {url}");

        return (true, webhook, null);
    }

    /// <summary>
    /// Get all webhooks for a user
    /// </summary>
    public async Task<List<Webhook>> GetUserWebhooksAsync(Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.Webhooks
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Delete a webhook
    /// </summary>
    public async Task<bool> DeleteWebhookAsync(Guid webhookId, Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var webhook = await dbContext.Webhooks
            .FirstOrDefaultAsync(w => w.Id == webhookId && w.UserId == userId);

        if (webhook == null)
            return false;

        dbContext.Webhooks.Remove(webhook);
        await dbContext.SaveChangesAsync();

        await _auditLogService.LogActionAsync(
            userId,
            AuditAction.Deleted,
            "Webhook",
            webhookId,
            $"Deleted webhook for {webhook.Url}");

        return true;
    }

    /// <summary>
    /// Get active webhooks for a specific event type
    /// </summary>
    public async Task<List<Webhook>> GetWebhooksForEventAsync(Guid userId, WebhookEventType eventType)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var webhooks = await dbContext.Webhooks
            .Where(w => w.UserId == userId && w.IsActive)
            .ToListAsync();

        return webhooks
            .Where(w => {
                var events = JsonSerializer.Deserialize<List<WebhookEventType>>(w.EventsJson);
                return events?.Contains(eventType) ?? false;
            })
            .ToList();
    }
}
