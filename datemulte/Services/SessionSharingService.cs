using Datemulte_2.Data;
using Datemulte_2.Models;
using Datemulte_2.Models.DataManagement;
using Datemulte_2.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace Datemulte_2.Services;

/// <summary>
/// Manages session sharing and collaborative access
/// </summary>
public class SessionSharingService
{
    private readonly IDbContextFactory<DataManagementDbContext> _dbContextFactory;
private readonly AuditLogService _auditLogService;
private readonly EmailService _emailService;
private readonly IHttpContextAccessor _httpContextAccessor;

public SessionSharingService(
        IDbContextFactory<DataManagementDbContext> dbContextFactory,
        AuditLogService auditLogService,
        EmailService emailService,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContextFactory = dbContextFactory;
        _auditLogService = auditLogService;
        _emailService = emailService;
        _httpContextAccessor = httpContextAccessor;
    }

    private string GenerateSecureRandomToken()
    {
        var bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// Share a session with another user
    /// </summary>
    public async Task<(bool success, string message, SessionShare? share)> ShareSessionAsync(
        Guid sessionId,
        Guid ownerUserId,
        Guid sharedWithUserId,
        SessionPermission permission)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Verify owner actually owns the session
        var session = await dbContext.DataSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.UserId == ownerUserId);

        if (session == null)
            return (false, "Session not found or you don't have permission to share it", null);

        // Check if share already exists
        var existingShare = await dbContext.SessionShares
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.SharedWithUserId == sharedWithUserId);

        if (existingShare != null)
        {
            // Update existing permission
            existingShare.Permission = permission;
            await dbContext.SaveChangesAsync();

            await _auditLogService.LogActionAsync(
                ownerUserId,
                AuditAction.PermissionChanged,
                "SessionShare",
                existingShare.Id,
                $"Updated share permission to {permission}");

            return (true, "Share permission updated", existingShare);
        }

        // Create new share
        var share = new SessionShare
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            SharedByUserId = ownerUserId,
            SharedWithUserId = sharedWithUserId,
            Permission = permission,
            SharedAt = DateTime.UtcNow
        };

        dbContext.SessionShares.Add(share);
        await dbContext.SaveChangesAsync();

        await _auditLogService.LogActionAsync(
            ownerUserId,
            AuditAction.Shared,
            "Session",
            sessionId,
            $"Shared with user {sharedWithUserId} with {permission} permission");

        return (true, "Session shared successfully", share);
    }

    /// <summary>
    /// Revoke a user's access to a session
    /// </summary>
    public async Task<(bool success, string message)> RevokeAccessAsync(
        Guid shareId,
        Guid requestingUserId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var share = await dbContext.SessionShares
            .Include(s => s.Session)
            .FirstOrDefaultAsync(s => s.Id == shareId);

        if (share == null)
            return (false, "Share not found");

        // Only the session owner or the person who created the share can revoke it
        if (share.Session.UserId != requestingUserId && share.SharedByUserId != requestingUserId)
            return (false, "You don't have permission to revoke this share");

        dbContext.SessionShares.Remove(share);
        await dbContext.SaveChangesAsync();

        await _auditLogService.LogActionAsync(
            requestingUserId,
            AuditAction.PermissionChanged,
            "SessionShare",
            shareId,
            $"Revoked access for user {share.SharedWithUserId}");

        return (true, "Access revoked successfully");
    }

    /// <summary>
    /// Get all shares for a specific session
    /// </summary>
    public async Task<List<SessionShare>> GetSessionSharesAsync(Guid sessionId, Guid requestingUserId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Verify user has access to this session
        var hasAccess = await HasAccessAsync(sessionId, requestingUserId);
        if (!hasAccess)
            return new List<SessionShare>();

        return await dbContext.SessionShares
            .Include(s => s.SharedWithUser)
            .Include(s => s.SharedByUser)
            .Where(s => s.SessionId == sessionId)
            .OrderByDescending(s => s.SharedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get all sessions shared with a user
    /// </summary>
    public async Task<List<DataSession>> GetSharedWithMeAsync(Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.SessionShares
            .Where(s => s.SharedWithUserId == userId)
            .Include(s => s.Session)
            .Select(s => s.Session)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
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

    public async Task<(bool success, string message)> ShareSessionByEmailAsync(
        Guid sessionId,
        Guid ownerUserId,
        string email,
        SessionPermission permission,
        string sessionName)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        string inviteToken = GenerateSecureRandomToken();
        var invitation = new SessionInvitation
        {
            SessionId = sessionId,
            Email = email.Trim(),
            InviteToken = inviteToken,
            Permission = permission
        };
        dbContext.SessionInvitations.Add(invitation);
        await dbContext.SaveChangesAsync();

        var request = _httpContextAccessor.HttpContext?.Request;
        string baseUrl = request != null
            ? $"{request.Scheme}://{request.Host}"
            : "http://localhost";

        string invitationUrl = $"{baseUrl}/share/invite/{inviteToken}";
        var (emailSuccess, emailError) = await _emailService.SendInvitationEmailAsync(email, sessionName, invitationUrl);

        if (!emailSuccess)
        {
            return (false, $"Invitation created but email failed to send: {emailError}");
        }

        return (true, "Invitation sent to email.");
    }
}
