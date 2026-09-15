using Datemulte_2.Data;
using Datemulte_2.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Datemulte_2.Services.Security;

/// <summary>
/// Service for logging security and audit events
/// </summary>
public class AuditLogService
{
    private readonly IDbContextFactory<DataManagementDbContext> _dbContextFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogService(
        IDbContextFactory<DataManagementDbContext> dbContextFactory,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContextFactory = dbContextFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Log an audit event
    /// </summary>
    public async Task LogActionAsync(
        Guid? userId,
        AuditAction action,
        string entityType,
        Guid? entityId,
        string details)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var httpContext = _httpContextAccessor.HttpContext;
        var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = httpContext?.Request.Headers["User-Agent"].ToString() ?? "Unknown";

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DetailsJson = JsonSerializer.Serialize(new { message = details }),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.AuditLogs.Add(auditLog);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Log an audit event with structured details
    /// </summary>
    public async Task LogActionWithDetailsAsync<T>(
        Guid? userId,
        AuditAction action,
        string entityType,
        Guid? entityId,
        T details) where T : class
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var httpContext = _httpContextAccessor.HttpContext;
        var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = httpContext?.Request.Headers["User-Agent"].ToString() ?? "Unknown";

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DetailsJson = JsonSerializer.Serialize(details),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.AuditLogs.Add(auditLog);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Get audit logs for a specific entity
    /// </summary>
    public async Task<List<AuditLog>> GetEntityLogsAsync(string entityType, Guid entityId, int limit = 100)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.AuditLogs
            .Include(a => a.User)
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// Get audit logs for a specific user
    /// </summary>
    public async Task<List<AuditLog>> GetUserLogsAsync(Guid userId, int limit = 100)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.AuditLogs
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// Get recent audit logs (admin view)
    /// </summary>
    public async Task<List<AuditLog>> GetRecentLogsAsync(int limit = 100)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.AuditLogs
            .Include(a => a.User)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// Clean up old audit logs (keep last 90 days)
    /// </summary>
    public async Task<int> CleanupOldLogsAsync(int daysToKeep = 90)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
        
        var oldLogs = await dbContext.AuditLogs
            .Where(a => a.CreatedAt < cutoffDate)
            .ToListAsync();

        dbContext.AuditLogs.RemoveRange(oldLogs);
        await dbContext.SaveChangesAsync();

        return oldLogs.Count;
    }
}
