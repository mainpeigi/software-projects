namespace Datemulte_2.Models;

public class NotificationPreference
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public bool EmailEnabled { get; set; } = false;
    public bool InAppEnabled { get; set; } = true;
    public bool WebhookEnabled { get; set; } = false;
    public UserProfile User { get; set; } = null!;
}