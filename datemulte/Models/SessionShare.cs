using Datemulte_2.Models.DataManagement;

namespace Datemulte_2.Models;

/// <summary>
/// Represents sharing permissions for a data session
/// Enables collaboration by allowing session owners to share with other users
/// </summary>
public class SessionShare
{
    public Guid Id { get; set; }
    
    // Foreign keys
    public Guid SessionId { get; set; }
    public Guid SharedWithUserId { get; set; }
    public Guid SharedByUserId { get; set; }
    
    // Permission level
    public SessionPermission Permission { get; set; } = SessionPermission.Viewer;
    
    // Sharing metadata
    public DateTime SharedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public DataSession Session { get; set; } = null!;
    public UserProfile SharedWithUser { get; set; } = null!;
    public UserProfile SharedByUser { get; set; } = null!;
}

/// <summary>
/// Permission levels for shared sessions
/// </summary>
public enum SessionPermission
{
    Viewer = 0,      // View only - can see data but not modify
    Commenter = 1,   // View + comment - can add comments/discussions
    Editor = 2,      // View + comment + edit - can modify data
    Admin = 3        // Full control - can delete, share with others, manage permissions
}
