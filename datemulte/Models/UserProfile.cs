namespace Datemulte_2.Models;

/// <summary>
/// Represents a user's profile information and preferences
/// Stores display settings, avatar, 2FA configuration, and notification preferences
/// </summary>
public class UserProfile
{
    // Primary key for the user profile
    public Guid Id { get; set; }
    // Foreign key linking to supabase auth user ID
    public Guid UserId { get; set; }
    
    // Basic profile info
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Email { get; set; }
    
    // Security
    public bool TwoFactorEnabled { get; set; } = false;
    public string? TwoFactorSecret { get; set; }
    
    // NEW: Enhanced profile fields
    public string? Bio { get; set; }  // Max 500 chars
    public string? Pronouns { get; set; }  // e.g., "he/him", "she/her", "they/them"
    public string? Timezone { get; set; }  // IANA timezone, e.g., "America/New_York"
    public string? Language { get; set; } = "en-US";  // ISO language code
    
    // NEW: Accessibility settings
    public bool ReducedMotion { get; set; } = false;
    public bool ScreenReaderOptimized { get; set; } = false;
    public int FontSizeMultiplier { get; set; } = 100;  // Percentage
    
    // Notifications
    public bool EmailNotifications { get; set; } = false;
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // NEW: Navigation properties for relationships
    public ThemeConfiguration? Theme { get; set; }
    public ICollection<SessionShare> SharedSessions { get; set; } = new List<SessionShare>();
    public ICollection<SessionShare> SessionsSharedByMe { get; set; } = new List<SessionShare>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<NotificationPreference> NotificationPreferences { get; set; } = new List<NotificationPreference>();
    public ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
    public ICollection<Webhook> Webhooks { get; set; } = new List<Webhook>();
    public ICollection<SessionComment> Comments { get; set; } = new List<SessionComment>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<ScheduledReport> ScheduledReports { get; set; } = new List<ScheduledReport>();
}
