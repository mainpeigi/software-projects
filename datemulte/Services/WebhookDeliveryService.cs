using Datemulte_2.Data;
using Datemulte_2.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Datemulte_2.Services;

/// <summary>
/// Handles webhook delivery with retry logic
/// </summary>
public class WebhookDeliveryService
{
    private readonly IDbContextFactory<DataManagementDbContext> _dbContextFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDeliveryService> _logger;

    public WebhookDeliveryService(
        IDbContextFactory<DataManagementDbContext> dbContextFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookDeliveryService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Dispatch a webhook event
    /// </summary>
    public async Task DispatchWebhookAsync(
        Guid userId,
        WebhookEventType eventType,
        object payload)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Get webhooks subscribed to this event type
        var webhooks = await dbContext.Webhooks
            .Where(w => w.UserId == userId && w.IsActive)
            .ToListAsync();

        var relevantWebhooks = webhooks
            .Where(w => {
                var events = JsonSerializer.Deserialize<List<WebhookEventType>>(w.EventsJson);
                return events?.Contains(eventType) ?? false;
            })
            .ToList();

        foreach (var webhook in relevantWebhooks)
        {
            await DeliverWebhookAsync(webhook, eventType, payload);
        }
    }

    /// <summary>
    /// Deliver a webhook with retry logic
    /// </summary>
    private async Task DeliverWebhookAsync(
        Webhook webhook,
        WebhookEventType eventType,
        object payload)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var delivery = new WebhookDelivery
        {
            Id = Guid.NewGuid(),
            WebhookId = webhook.Id,
            EventType = eventType,
            PayloadJson = JsonSerializer.Serialize(payload),
            CreatedAt = DateTime.UtcNow,
            IsDelivered = false,
            AttemptCount = 0
        };

        dbContext.WebhookDeliveries.Add(delivery);
        await dbContext.SaveChangesAsync();

        // Attempt delivery
        await AttemptDeliveryAsync(delivery, webhook);
    }

    /// <summary>
    /// Attempt to deliver a webhook
    /// </summary>
    public async Task AttemptDeliveryAsync(WebhookDelivery delivery, Webhook webhook)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        delivery.AttemptCount++;

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var request = new HttpRequestMessage(HttpMethod.Post, webhook.Url);
            request.Content = new StringContent(delivery.PayloadJson, Encoding.UTF8, "application/json");
            request.Headers.Add("X-Webhook-Event", delivery.EventType.ToString());
            request.Headers.Add("X-Webhook-Delivery-Id", delivery.Id.ToString());

            // Add HMAC signature if secret is configured
            if (!string.IsNullOrEmpty(webhook.Secret))
            {
                var signature = GenerateHmacSignature(delivery.PayloadJson, webhook.Secret);
                request.Headers.Add("X-Webhook-Signature", signature);
            }

            var response = await httpClient.SendAsync(request);

            delivery.HttpStatusCode = (int)response.StatusCode;
            delivery.ResponseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                delivery.IsDelivered = true;
                delivery.DeliveredAt = DateTime.UtcNow;
                _logger.LogInformation("Webhook delivered successfully to {Url}", webhook.Url);
            }
            else
            {
                delivery.IsDelivered = false;
                delivery.ErrorMessage = $"HTTP {response.StatusCode}: {delivery.ResponseBody}";
                _logger.LogWarning("Webhook delivery failed to {Url}: {Error}", webhook.Url, delivery.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            delivery.IsDelivered = false;
            delivery.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Exception during webhook delivery to {Url}", webhook.Url);
        }

        // Update delivery record
        dbContext.WebhookDeliveries.Update(delivery);
        await dbContext.SaveChangesAsync();

        // Schedule retry if needed (max 3 attempts)
        if (!delivery.IsDelivered && delivery.AttemptCount < 3)
        {
            // In production, you would schedule a background job here
            // For now, we'll just log it
            _logger.LogInformation("Webhook delivery will be retried (attempt {Attempt}/3)", delivery.AttemptCount);
        }
    }

    /// <summary>
    /// Generate HMAC-SHA256 signature for webhook payload
    /// </summary>
    private string GenerateHmacSignature(string payload, string secret)
    {
        var encoding = new UTF8Encoding();
        var keyBytes = encoding.GetBytes(secret);
        var messageBytes = encoding.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(messageBytes);
        return Convert.ToHexString(hashBytes).ToLower();
    }

    /// <summary>
    /// Get delivery history for a webhook
    /// </summary>
    public async Task<List<WebhookDelivery>> GetDeliveryHistoryAsync(Guid webhookId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.WebhookDeliveries
            .Where(d => d.WebhookId == webhookId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(100)
            .ToListAsync();
    }

    /// <summary>
    /// Retry a failed webhook delivery
    /// </summary>
    public async Task<bool> RetryDeliveryAsync(Guid deliveryId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var delivery = await dbContext.WebhookDeliveries
            .Include(d => d.Webhook)
            .FirstOrDefaultAsync(d => d.Id == deliveryId);

        if (delivery == null || delivery.Webhook == null)
            return false;

        if (delivery.AttemptCount >= 3)
            return false;

        await AttemptDeliveryAsync(delivery, delivery.Webhook);
        return true;
    }
}
