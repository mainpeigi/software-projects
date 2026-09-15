namespace Datemulte_2.Models;

public class ApiKey
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public string KeyHash { get; set; } = "";
    public string KeyPrefix { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public int RateLimitPerMinute { get; set; } = 60;
    public int RateLimitPerDay { get; set; } = 10000;
    public string Scopes { get; set; } = "read";
    public UserProfile User { get; set; } = null!;
}
