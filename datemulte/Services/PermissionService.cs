using Datemulte_2.Data;
using Datemulte_2.Models;
using Microsoft.EntityFrameworkCore;

namespace Datemulte_2.Services;

/// <summary>
/// Centralized permission checking for session access control
/// </summary>
public class PermissionService
{
    private readonly IDbContextFactory<DataManagementDbContext> _dbContextFactory;

    public PermissionService(IDbContextFactory<DataManagementDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Check if a user has access to a session
    /// </summary>
    public async Task<bool> HasAccessAsync(Guid sessionId, Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Check if user owns the session
        var ownsSession = await dbContext.DataSessions
            .AnyAsync(s => s.SessionId == sessionId && s.UserId == userId);

        if (ownsSession)
            return true;

        // Check if session is shared with user
        var hasShare = await dbContext.SessionShares
            .AnyAsync(s => s.SessionId == sessionId && s.SharedWithUserId == userId);

        return hasShare;
    }

    /// <summary>
    /// Get user's permission level for a session
    /// </summary>
    public async Task<SessionPermission?> GetPermissionAsync(Guid sessionId, Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Check if user owns the session (Admin permission)
        var ownsSession = await dbContext.DataSessions
            .AnyAsync(s => s.SessionId == sessionId && s.UserId == userId);

        if (ownsSession)
            return SessionPermission.Admin;

        // Check shared permission
        var share = await dbContext.SessionShares
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.SharedWithUserId == userId);

        return share?.Permission;
    }

    /// <summary>
    /// Check if user can edit a session
    /// </summary>
    public async Task<bool> CanEditAsync(Guid sessionId, Guid userId)
    {
        var permission = await GetPermissionAsync(sessionId, userId);
        return permission >= SessionPermission.Editor;
    }

    /// <summary>
    /// Check if user can comment on a session
    /// </summary>
    public async Task<bool> CanCommentAsync(Guid sessionId, Guid userId)
    {
        var permission = await GetPermissionAsync(sessionId, userId);
        return permission >= SessionPermission.Commenter;
    }

    /// <summary>
    /// Check if user can view a session
    /// </summary>
    public async Task<bool> CanViewAsync(Guid sessionId, Guid userId)
    {
        var permission = await GetPermissionAsync(sessionId, userId);
        return permission >= SessionPermission.Viewer;
    }

    /// <summary>
    /// Check if user is admin of a session
    /// </summary>
    public async Task<bool> IsAdminAsync(Guid sessionId, Guid userId)
    {
        var permission = await GetPermissionAsync(sessionId, userId);
        return permission == SessionPermission.Admin;
    }
}
