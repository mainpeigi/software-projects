using Datemulte_2.Services;
using Datemulte_2.Services.DataManagement;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Datemulte_2.Controllers.Api;

[ApiController]
[Route("api/v1/sessions/{sessionId}/data")]
public class DataApiController : ControllerBase
{
    private readonly DataSessionService _sessionService;
    private readonly PermissionService _permissionService;

    public DataApiController(
        DataSessionService sessionService,
        PermissionService permissionService)
    {
        _sessionService = sessionService;
        _permissionService = permissionService;
    }

    /// <summary>
    /// Get data rows for a session
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetData(
        Guid sessionId,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 1000)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var hasAccess = await _permissionService.HasAccessAsync(sessionId, userId.Value);
        if (!hasAccess)
            return Forbid();

        var (session, allData) = await _sessionService.LoadSessionAsync(sessionId, userId.Value);

        var pagedData = allData
            .Skip(offset)
            .Take(Math.Min(limit, 1000))
            .ToList();

        return Ok(new
        {
            sessionId = session.SessionId,
            sessionName = session.SessionName,
            offset,
            limit,
            total = allData.Count,
            data = pagedData
        });
    }

    /// <summary>
    /// Add data rows to a session
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddData(
        Guid sessionId,
        [FromBody] List<Dictionary<string, object>> rows)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var canEdit = await _permissionService.CanEditAsync(sessionId, userId.Value);
        if (!canEdit)
            return Forbid();

        await _sessionService.AppendDataAsync(sessionId, rows, userId.Value);

        return Ok(new { message = "Data added successfully", rowsAdded = rows.Count });
    }

    /// <summary>
    /// Update a data row
    /// </summary>
    [HttpPut("{rowIndex}")]
    public async Task<IActionResult> UpdateRow(
        Guid sessionId,
        int rowIndex,
        [FromBody] Dictionary<string, object> row)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var canEdit = await _permissionService.CanEditAsync(sessionId, userId.Value);
        if (!canEdit)
            return Forbid();

        await _sessionService.UpdateRowAsync(sessionId, rowIndex, row, userId.Value);

        return Ok(new { message = "Row updated successfully" });
    }

    /// <summary>
    /// Delete a data row
    /// </summary>
    [HttpDelete("{rowIndex}")]
    public async Task<IActionResult> DeleteRow(
        Guid sessionId,
        int rowIndex)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var canEdit = await _permissionService.CanEditAsync(sessionId, userId.Value);
        if (!canEdit)
            return Forbid();

        await _sessionService.DeleteRowAsync(sessionId, rowIndex, userId.Value);

        return NoContent();
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return userIdClaim != null ? Guid.Parse(userIdClaim) : null;
    }
}
