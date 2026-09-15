using Datemulte_2.Data;
using Datemulte_2.Models;
using Datemulte_2.Services.Security;
using Microsoft.EntityFrameworkCore;

namespace Datemulte_2.Services;

/// <summary>
/// Manages comments and discussions on data sessions
/// </summary>
public class CommentService
{
    private readonly IDbContextFactory<DataManagementDbContext> _dbContextFactory;
    private readonly PermissionService _permissionService;
    private readonly AuditLogService _auditLogService;
    private readonly NotificationDispatcher _notificationDispatcher;

    public CommentService(
        IDbContextFactory<DataManagementDbContext> dbContextFactory,
        PermissionService permissionService,
        AuditLogService auditLogService,
        NotificationDispatcher notificationDispatcher)
    {
        _dbContextFactory = dbContextFactory;
        _permissionService = permissionService;
        _auditLogService = auditLogService;
        _notificationDispatcher = notificationDispatcher;
    }

    /// <summary>
    /// Add a comment to a session
    /// </summary>
    public async Task<(bool success, string message, SessionComment? comment)> AddCommentAsync(
        Guid sessionId,
        Guid userId,
        string content,
        Guid? parentCommentId = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Check if user has at least Commenter permission
        var permission = await _permissionService.GetPermissionAsync(sessionId, userId);
        if (permission == null || permission < SessionPermission.Commenter)
            return (false, "You don't have permission to comment on this session", null);

        // Validate parent comment exists if specified
        if (parentCommentId.HasValue)
        {
            var parentExists = await dbContext.SessionComments
                .AnyAsync(c => c.Id == parentCommentId.Value && c.SessionId == sessionId);

            if (!parentExists)
                return (false, "Parent comment not found", null);
        }

        // Ensure user profile exists (required for foreign key)
        var userProfile = await dbContext.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);
        if (userProfile == null)
        {
            userProfile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DisplayName = "User",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.UserProfiles.Add(userProfile);
            await dbContext.SaveChangesAsync();
        }

        var comment = new SessionComment
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = userId,
            CommentText = content,
            ParentCommentId = parentCommentId,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.SessionComments.Add(comment);
        await dbContext.SaveChangesAsync();

        await _auditLogService.LogActionAsync(
            userId,
            AuditAction.Created,
            "SessionComment",
            comment.Id,
            $"Added comment to session {sessionId}");

        // Get session details for notification
        var session = await dbContext.DataSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
        var user = await dbContext.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);
        
        if (session != null && user != null)
        {
            try
            {
                var commenterName = user.DisplayName ?? "A user";
                
                // Notify session owner (if not the commenter)
                if (session.UserId != userId)
                {
                    await _notificationDispatcher.NotifyCommentAddedAsync(
                        session.UserId,
                        commenterName,
                        session.SessionName,
                        sessionId,
                        content);
                }
            }
            catch (Exception)
            {
                // Silently fail notification - don't block comment creation
            }
        }

        return (true, "Comment added successfully", comment);
    }

    /// <summary>
    /// Update a comment
    /// </summary>
    public async Task<(bool success, string message)> UpdateCommentAsync(
        Guid commentId,
        Guid userId,
        string newContent)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var comment = await dbContext.SessionComments
            .FirstOrDefaultAsync(c => c.Id == commentId);

        if (comment == null)
            return (false, "Comment not found");

        // Only the comment author can update it
        if (comment.UserId != userId)
            return (false, "You can only edit your own comments");

        comment.CommentText = newContent;
        comment.EditedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        await _auditLogService.LogActionAsync(
            userId,
            AuditAction.Updated,
            "SessionComment",
            commentId,
            "Updated comment content");

        return (true, "Comment updated successfully");
    }

    /// <summary>
    /// Delete a comment
    /// </summary>
    public async Task<(bool success, string message)> DeleteCommentAsync(
        Guid commentId,
        Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var comment = await dbContext.SessionComments
            .Include(c => c.Session)
            .FirstOrDefaultAsync(c => c.Id == commentId);

        if (comment == null)
            return (false, "Comment not found");

        // User can delete if they're the author OR have Admin permission on the session
        var canDelete = comment.UserId == userId;
        
        if (!canDelete)
        {
            var permission = await _permissionService.GetPermissionAsync(comment.SessionId, userId);
            canDelete = permission == SessionPermission.Admin;
        }

        if (!canDelete)
            return (false, "You don't have permission to delete this comment");

        dbContext.SessionComments.Remove(comment);
        await dbContext.SaveChangesAsync();

        await _auditLogService.LogActionAsync(
            userId,
            AuditAction.Deleted,
            "SessionComment",
            commentId,
            $"Deleted comment from session {comment.SessionId}");

        return (true, "Comment deleted successfully");
    }

    /// <summary>
    /// Get all comments for a session (with nested replies)
    /// </summary>
    public async Task<List<SessionComment>> GetSessionCommentsAsync(Guid sessionId, Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Check if user has access to the session
        var hasAccess = await _permissionService.HasAccessAsync(sessionId, userId);
        if (!hasAccess)
            return new List<SessionComment>();

        return await dbContext.SessionComments
            .Include(c => c.User)
            .Include(c => c.Replies)
                .ThenInclude(r => r.User)
            .Where(c => c.SessionId == sessionId && c.ParentCommentId == null)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get comment count for a session
    /// </summary>
    public async Task<int> GetCommentCountAsync(Guid sessionId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.SessionComments
            .CountAsync(c => c.SessionId == sessionId);
    }
}
