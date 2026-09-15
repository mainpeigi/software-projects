namespace Datemulte_2.Models;

/// <summary>
/// Log of webhook delivery attempts with retry support
/// </summary>
public class WebhookDelivery
{
    public Guid Id { get; set; }
    public Guid WebhookId { get; set; }
    
    // Event details
    public WebhookEventType EventType { get; set; }
    public string PayloadJson { get; set; } = "{}";
    
    // Delivery status
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }
    
    public int AttemptCount { get; set; } = 0;
    public bool IsDelivered { get; set; } = false;
    public string? ErrorMessage { get; set; }
    
    // Response details
    public int HttpStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    
    // Navigation
    public Webhook Webhook { get; set; } = null!;
}
