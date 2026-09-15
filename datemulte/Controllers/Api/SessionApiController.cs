using Datemulte_2.Services;
using Datemulte_2.Services.DataManagement;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Datemulte_2.Controllers.Api;

[ApiController]
[Route("api/v1/sessions")]
public class SessionsApiController : ControllerBase
{
    private readonly DataSessionService _sessionService;
    private readonly PermissionService _permissionService;

    public SessionsApiController(
        DataSessionService sessionService,
        PermissionService permissionService)
    {
        _sessionService = sessionService;
        _permissionService = permissionService;
    }

    /// <summary>
    /// Get all sessions for the authenticated user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSessions()
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var sessions = await _sessionService.GetUserSessionsAsync(userId.Value);
        
        return Ok(new
        {
            sessions = sessions.Select(s => new
            {
                id = s.SessionId,
                name = s.SessionName,
                createdAt = s.CreatedAt,
                lastModifiedAt = s.LastModifiedAt,
                columns = s.Columns,
                numericColumns = s.NumericColumns
            })
        });
    }

    /// <summary>
    /// Get a specific session by ID
    /// </summary>
    [HttpGet("{sessionId}")]
    public async Task<IActionResult> GetSession(Guid sessionId)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        // Check permissions
        var hasAccess = await _permissionService.HasAccessAsync(sessionId, userId.Value);
        if (!hasAccess)
            return Forbid();

        var (session, data) = await _sessionService.LoadSessionAsync(sessionId, userId.Value);

        return Ok(new
        {
            id = session.SessionId,
            name = session.SessionName,
            createdAt = session.CreatedAt,
            lastModifiedAt = session.LastModifiedAt,
            columns = session.Columns,
            numericColumns = session.NumericColumns,
            timeColumn = session.TimeColumn,
            columnUnits = session.ColumnUnits,
            sensorGroups = session.SensorGroups,
            rowCount = data.Count
        });
    }

    /// <summary>
    /// Delete a session
    /// </summary>
    [HttpDelete("{sessionId}")]
    public async Task<IActionResult> DeleteSession(Guid sessionId)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        // Check if user is admin of the session
        var isAdmin = await _permissionService.IsAdminAsync(sessionId, userId.Value);
        if (!isAdmin)
            return Forbid();

        await _sessionService.DeleteSessionAsync(sessionId, userId.Value);

        return NoContent();
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return userIdClaim != null ? Guid.Parse(userIdClaim) : null;
    }
}
