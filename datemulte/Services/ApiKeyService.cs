using Datemulte_2.Data;
using Datemulte_2.Models;
using Datemulte_2.Services.Security;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Datemulte_2.Services;

/// <summary>
/// Manages API keys for external integrations
/// </summary>
public class ApiKeyService
{
    private readonly IDbContextFactory<DataManagementDbContext> _dbContextFactory;
    private readonly AuditLogService _auditLogService;

    public ApiKeyService(
        IDbContextFactory<DataManagementDbContext> dbContextFactory,
        AuditLogService auditLogService)
    {
        _dbContextFactory = dbContextFactory;
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// Create a new API key for a user
    /// </summary>
    public async Task<(bool success, string? key, string? error)> CreateApiKeyAsync(
        Guid userId,
        string name,
        DateTime? expiresAt = null,
        int? rateLimitPerMinute = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Generate secure random key
        var keyBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(keyBytes);
        }
        var key = Convert.ToBase64String(keyBytes);

        // Hash the key for storage
        var keyHash = HashKey(key);

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            KeyHash = keyHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            IsActive = true,
            RateLimitPerMinute = rateLimitPerMinute ?? 60,
            LastUsedAt = null
        };

        dbContext.ApiKeys.Add(apiKey);
        await dbContext.SaveChangesAsync();

        await _auditLogService.LogActionAsync(
            userId,
            AuditAction.Created,
            "ApiKey",
            apiKey.Id,
            $"Created API key: {name}");

        // Return the unhashed key (only time it's visible)
        return (true, key, null);
    }

    /// <summary>
    /// Validate an API key and return the associated user
    /// </summary>
    public async Task<(bool isValid, Guid? userId, ApiKey? apiKey)> ValidateApiKeyAsync(string key)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var keyHash = HashKey(key);

        var apiKey = await dbContext.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsActive);

        if (apiKey == null)
            return (false, null, null);

        // Check expiration
        if (apiKey.ExpiresAt.HasValue && apiKey.ExpiresAt.Value < DateTime.UtcNow)
            return (false, null, null);

        // Update last used timestamp
        apiKey.LastUsedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return (true, apiKey.UserId, apiKey);
    }

    /// <summary>
    /// Revoke an API key
    /// </summary>
    public async Task<bool> RevokeApiKeyAsync(Guid apiKeyId, Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var apiKey = await dbContext.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == apiKeyId && k.UserId == userId);

        if (apiKey == null)
            return false;

        apiKey.IsActive = false;
        await dbContext.SaveChangesAsync();

        await _auditLogService.LogActionAsync(
            userId,
            AuditAction.Deleted,
            "ApiKey",
            apiKeyId,
            $"Revoked API key: {apiKey.Name}");

        return true;
    }

    /// <summary>
    /// Get all API keys for a user
    /// </summary>
    public async Task<List<ApiKey>> GetUserApiKeysAsync(Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.ApiKeys
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Hash an API key for secure storage
    /// </summary>
    private string HashKey(string key)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(hashBytes);
    }
}
