namespace Datemulte_2.Models;

/// <summary>
/// Webhook subscription for receiving event notifications
/// </summary>
public class Webhook
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    
    // Webhook configuration
    public string Url { get; set; } = "";
    public string Secret { get; set; } = "";  // For HMAC signature verification
    
    // Event subscriptions (JSON array of WebhookEventType)
    public string EventsJson { get; set; } = "[]";
    
    // Status
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastTriggeredAt { get; set; }
    
    // Delivery statistics
    public int SuccessCount { get; set; } = 0;
    public int FailureCount { get; set; } = 0;
    
    // Navigation
    public UserProfile User { get; set; } = null!;
    public ICollection<WebhookDelivery> Deliveries { get; set; } = new List<WebhookDelivery>();
}

/// <summary>
/// Types of events that can trigger webhooks
/// </summary>
public enum WebhookEventType
{
    SessionCreated,
    SessionUpdated,
    SessionDeleted,
    DataChanged,
    SessionShared,
    CommentAdded
}
