namespace Datemulte_2.Models;

public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string? ActionUrl { get; set; }
    public string ContextJson { get; set; } = "{}";
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public UserProfile User { get; set; } = null!;
}

public enum NotificationType
{
    SessionShared,
    CommentAdded,
    DataChanged,
    SystemAlert,
    ReportReady,
    PermissionChanged,
    SessionDeleted
}