namespace Datemulte_2.Models;

/// <summary>
/// Comprehensive audit trail for security and compliance
/// Tracks all user actions and system events
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }  // Nullable for system events
    
    // Action details
    public AuditAction Action { get; set; }
    public string EntityType { get; set; } = "";  // "Session", "User", "ApiKey", etc.
    public Guid? EntityId { get; set; }
    
    // Request context
    public string IpAddress { get; set; } = "";
    public string UserAgent { get; set; } = "";
    
    // Detailed change information (JSON)
    public string DetailsJson { get; set; } = "{}";
    
    // Timestamp
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation (nullable for system events)
    public UserProfile? User { get; set; }
}

/// <summary>
/// Types of auditable actions
/// </summary>
public enum AuditAction
{
    Created,
    Updated,
    Deleted,
    Viewed,
    Shared,
    LoginSuccess,
    LoginFailure,
    ApiAccess,
    PermissionChanged,
    ExportData
}
