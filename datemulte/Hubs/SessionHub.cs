using Datemulte_2.Services;
using Microsoft.AspNetCore.SignalR;

namespace Datemulte_2.Hubs;

public class SessionHub : Hub
{
    private readonly PermissionService _permissionService;
    public SessionHub(PermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    public async Task JoinSession(string sessionId, string userId)
    {
        if(!Guid.TryParse(sessionId, out var sessionGuid) || !Guid.TryParse(userId, out var userGuid))
        {
            return;
        }

        var hasAccess = await _permissionService.HasAccessAsync(sessionGuid, userGuid);
        if(!hasAccess)
        {
            await Clients.Caller.SendAsync("Error", "You don't have access to this session");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"session_{sessionId}");

        await Clients.OthersInGroup($"session_{sessionId}").SendAsync("UserJoined", userId, DateTime.UtcNow);
    }

    public async Task LeaveSession(string sessionId, string userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session_{sessionId}");

        await Clients.OthersInGroup($"session_{sessionId}").SendAsync("UserLeft", userId, DateTime.UtcNow);
    }

    public async Task NotifyDataChanged(string sessionId, string userId, string changeType)
    {
        await Clients.OthersInGroup($"session_{sessionId}").SendAsync("DataChanged", userId, changeType, DateTime.UtcNow);
    }

    public async Task NotifyCommentUpdated(string sessionId, string commentId)
    {
        await Clients.OthersInGroup($"session_{sessionId}").SendAsync("CommentUpdated", commentId, DateTime.UtcNow);
    }

    public async Task NotifyCommentDeleted(string sessionId, string commentId)
    {
        await Clients.Group($"session_{sessionId}")
            .SendAsync("CommentDeleted", commentId, DateTime.UtcNow);
    }

    /// <summary>
    /// Send typing indicator
    /// </summary>
    public async Task NotifyTyping(string sessionId, string userId, bool isTyping)
    {
        await Clients.OthersInGroup($"session_{sessionId}")
            .SendAsync("UserTyping", userId, isTyping);
    }
    
    /// <summary>
    /// Broadcast cursor position (for collaborative editing)
    /// </summary>
    public async Task NotifyCursorPosition(string sessionId, string userId, int row, int column)
    {
        await Clients.OthersInGroup($"session_{sessionId}")
            .SendAsync("CursorMoved", userId, row, column);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Clean up any group memberships
        await base.OnDisconnectedAsync(exception);
    }
}