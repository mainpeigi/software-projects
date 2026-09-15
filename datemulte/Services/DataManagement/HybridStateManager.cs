using Datemulte_2.Data;
using Datemulte_2.Models;
using Datemulte_2.Models.DataManagement;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Datemulte_2.Services.DataManagement;

/// <summary>
/// Manages data session state using the Hybrid Snapshot + Event Log pattern.
/// Provides efficient loading, saving, and versioning of large datasets.
/// </summary>
public class HybridStateManager
{
    private readonly IDbContextFactory<DataManagementDbContext> _dbContextFactory;
    private readonly ILogger<HybridStateManager> _logger;

    // Create snapshot every N edits
    private const int SNAPSHOT_THRESHOLD = 50;

    public HybridStateManager(
        IDbContextFactory<DataManagementDbContext> dbContextFactory,
        ILogger<HybridStateManager> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Loads the current state of a session by loading the latest snapshot and applying delta events.
    /// </summary>
    public async Task<List<GenericCsvRow>> LoadCurrentStateAsync(Guid sessionId)
    {
        _logger.LogInformation("Loading current state for session {SessionId}", sessionId);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // 1. Load latest snapshot
        var snapshot = await dbContext.DataSnapshots
            .Where(s => s.SessionId == sessionId)
            .OrderByDescending(s => s.VersionNumber)
            .FirstOrDefaultAsync();

        if (snapshot == null)
        {
            _logger.LogWarning("No snapshot found for session {SessionId}", sessionId);
            return new List<GenericCsvRow>();
        }

        _logger.LogDebug("Loading snapshot version {Version} ({Rows} rows, {Size} bytes compressed)",
            snapshot.VersionNumber, snapshot.RowCount, snapshot.CompressedData.Length);

        // 2. Decompress and deserialize snapshot
        var data = DecompressAndDeserialize(snapshot);

        // 3. Apply delta events since snapshot
        var events = await dbContext.DataChangeEvents
            .Where(e => e.SessionId == sessionId && e.FromVersion == snapshot.VersionNumber)
            .OrderBy(e => e.EventSequence)
            .ToListAsync();

        _logger.LogDebug("Applying {EventCount} delta events", events.Count);

        foreach (var evt in events)
        {
            ApplyEvent(data, evt);
        }

        _logger.LogInformation("Loaded current state: {Rows} rows, {Events} events applied",
            data.Count, events.Count);

        return data;
    }

    /// <summary>
    /// Saves a data edit event and creates a new snapshot if threshold is reached.
    /// Uses a transaction to ensure consistency.
    /// </summary>
    public async Task SaveEditAsync(Guid sessionId, DataChangeEvent edit)
    {
        _logger.LogDebug("Saving edit for session {SessionId}: {EventType}", sessionId, edit.EventType);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            // 1. Load session metadata with row lock for concurrency
            var session = await dbContext.DataSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null)
            {
                throw new InvalidOperationException($"Session {sessionId} not found");
            }

            // 2. Save delta event
            edit.SessionId = sessionId;
            edit.FromVersion = session.CurrentVersion;
            edit.EventSequence = session.EventCountSinceSnapshot + 1;
            edit.CreatedAt = DateTime.UtcNow;

            await dbContext.DataChangeEvents.AddAsync(edit);

            session.EventCountSinceSnapshot++;
            session.LastModifiedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();

            _logger.LogDebug("Edit saved as event {EventId} (sequence {Sequence})",
                edit.EventId, edit.EventSequence);

            // 3. Check if snapshot needed
            if (session.EventCountSinceSnapshot >= SNAPSHOT_THRESHOLD)
            {
                _logger.LogInformation("Snapshot threshold reached ({Count}/{Threshold}), creating new snapshot",
                    session.EventCountSinceSnapshot, SNAPSHOT_THRESHOLD);

                await CreateSnapshotInternalAsync(sessionId, session, dbContext);
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Creates an initial snapshot from uploaded data.
    /// </summary>
    public async Task CreateInitialSnapshotAsync(Guid sessionId, List<GenericCsvRow> data)
    {
        _logger.LogInformation("Creating initial snapshot for session {SessionId} ({Rows} rows)",
            sessionId, data.Count);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var session = await dbContext.DataSessions.FindAsync(sessionId);
        if (session == null)
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        // Serialize and compress
        var json = JsonSerializer.Serialize(data);
        var compressed = CompressionUtilities.CompressGzip(json);
        var checksum = CompressionUtilities.ComputeSHA256(compressed);

        var compressionRatio = CompressionUtilities.GetCompressionRatio(json.Length, compressed.Length);
        _logger.LogInformation("Compression: {Original} bytes -> {Compressed} bytes ({Ratio:F1}% reduction)",
            json.Length, compressed.Length, compressionRatio);

        var snapshot = new DataSnapshot
        {
            SessionId = sessionId,
            VersionNumber = 1,
            RowCount = data.Count,
            CompressedData = compressed,
            UncompressedSize = json.Length,
            Checksum = checksum
        };

        await dbContext.DataSnapshots.AddAsync(snapshot);

        session.CurrentVersion = 1;
        session.EventCountSinceSnapshot = 0;
        session.LastSnapshotAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        _logger.LogInformation("Initial snapshot created successfully");
    }

    /// <summary>
    /// Creates a new snapshot of the current state (public entry point with its own transaction).
    /// </summary>
    public async Task CreateSnapshotAsync(Guid sessionId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            var session = await dbContext.DataSessions.FindAsync(sessionId);
            if (session == null)
            {
                throw new InvalidOperationException($"Session {sessionId} not found");
            }

            await CreateSnapshotInternalAsync(sessionId, session, dbContext);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Creates a new snapshot of the current state (internal, assumes caller manages transaction).
    /// Also cleans up orphaned events from previous versions.
    /// </summary>
    private async Task CreateSnapshotInternalAsync(Guid sessionId, DataSession session, DataManagementDbContext dbContext)
    {
        var startTime = DateTime.UtcNow;
        var previousVersion = session.CurrentVersion;

        // Load current state (with all events applied)
        var currentData = await LoadCurrentStateAsync(sessionId);

        // Compress and save
        var json = JsonSerializer.Serialize(currentData);
        var compressed = CompressionUtilities.CompressGzip(json);
        var checksum = CompressionUtilities.ComputeSHA256(compressed);

        var newVersion = session.CurrentVersion + 1;

        var snapshot = new DataSnapshot
        {
            SessionId = sessionId,
            VersionNumber = newVersion,
            RowCount = currentData.Count,
            CompressedData = compressed,
            UncompressedSize = json.Length,
            Checksum = checksum
        };

        await dbContext.DataSnapshots.AddAsync(snapshot);

        // Clean up events from the previous version (they're now baked into the new snapshot)
        var orphanedEvents = await dbContext.DataChangeEvents
            .Where(e => e.SessionId == sessionId && e.FromVersion == previousVersion)
            .ToListAsync();

        if (orphanedEvents.Count > 0)
        {
            dbContext.DataChangeEvents.RemoveRange(orphanedEvents);
            _logger.LogDebug("Cleaned up {Count} events from version {Version}", orphanedEvents.Count, previousVersion);
        }

        session.CurrentVersion = newVersion;
        session.EventCountSinceSnapshot = 0;
        session.LastSnapshotAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        var elapsed = DateTime.UtcNow - startTime;
        var compressionRatio = CompressionUtilities.GetCompressionRatio(json.Length, compressed.Length);

        _logger.LogInformation(
            "Snapshot v{Version} created in {Elapsed:F2}s: {Rows} rows, {Size} bytes ({Ratio:F1}% compression)",
            newVersion, elapsed.TotalSeconds, currentData.Count, compressed.Length, compressionRatio);
    }

    /// <summary>
    /// Decompresses and deserializes a snapshot.
    /// </summary>
    private List<GenericCsvRow> DecompressAndDeserialize(DataSnapshot snapshot)
    {
        // Verify checksum
        if (!CompressionUtilities.VerifyChecksum(snapshot.CompressedData, snapshot.Checksum))
        {
            throw new InvalidOperationException($"Snapshot {snapshot.SnapshotId} checksum verification failed");
        }

        var json = CompressionUtilities.DecompressGzipToString(snapshot.CompressedData);
        var data = JsonSerializer.Deserialize<List<GenericCsvRow>>(json);

        if (data == null)
        {
            throw new InvalidOperationException($"Failed to deserialize snapshot {snapshot.SnapshotId}");
        }

        return data;
    }

    /// <summary>
    /// Applies a single event to the dataset.
    /// </summary>
    private void ApplyEvent(List<GenericCsvRow> data, DataChangeEvent evt)
    {
        switch (evt.EventType)
        {
            case DataEventType.CellUpdate:
                ApplyCellUpdate(data, evt);
                break;

            case DataEventType.RowInsert:
                ApplyRowInsert(data, evt);
                break;

            case DataEventType.RowDelete:
                ApplyRowDelete(data, evt);
                break;

            case DataEventType.BulkUpdate:
            case DataEventType.ColumnTransform:
                ApplyBulkOperation(data, evt);
                break;

            default:
                _logger.LogWarning("Unknown event type: {EventType}", evt.EventType);
                break;
        }
    }

    private void ApplyCellUpdate(List<GenericCsvRow> data, DataChangeEvent evt)
    {
        if (evt.RowIndex == null || evt.ColumnName == null || evt.NewValue == null)
        {
            _logger.LogWarning("Invalid CellUpdate event {EventId}: missing required fields", evt.EventId);
            return;
        }

        if (evt.RowIndex < 0 || evt.RowIndex >= data.Count)
        {
            _logger.LogWarning("Invalid row index {RowIndex} in event {EventId}", evt.RowIndex, evt.EventId);
            return;
        }

        data[evt.RowIndex.Value].Data[evt.ColumnName] = evt.NewValue;
    }

    private void ApplyRowInsert(List<GenericCsvRow> data, DataChangeEvent evt)
    {
        if (evt.JsonPatch == null)
        {
            _logger.LogWarning("Invalid RowInsert event {EventId}: missing JsonPatch", evt.EventId);
            return;
        }

        var newRow = JsonSerializer.Deserialize<GenericCsvRow>(evt.JsonPatch);
        if (newRow == null)
        {
            _logger.LogWarning("Failed to deserialize row from event {EventId}", evt.EventId);
            return;
        }

        if (evt.RowIndex == null || evt.RowIndex >= data.Count)
        {
            data.Add(newRow);
        }
        else
        {
            data.Insert(evt.RowIndex.Value, newRow);
        }
    }

    private void ApplyRowDelete(List<GenericCsvRow> data, DataChangeEvent evt)
    {
        if (evt.RowIndex == null || evt.RowIndex < 0 || evt.RowIndex >= data.Count)
        {
            _logger.LogWarning("Invalid row index {RowIndex} in event {EventId}", evt.RowIndex, evt.EventId);
            return;
        }

        data.RemoveAt(evt.RowIndex.Value);
    }

    private void ApplyBulkOperation(List<GenericCsvRow> data, DataChangeEvent evt)
    {
        if (evt.JsonPatch == null)
        {
            _logger.LogWarning("Invalid BulkOperation event {EventId}: missing JsonPatch", evt.EventId);
            return;
        }

        try
        {
            var bulkOp = JsonSerializer.Deserialize<BulkOperationPatch>(evt.JsonPatch);
            if (bulkOp == null)
            {
                _logger.LogWarning("Failed to deserialize bulk operation from event {EventId}", evt.EventId);
                return;
            }

            switch (bulkOp.Operation)
            {
                case "update_column":
                    // Update all values in a column
                    if (!string.IsNullOrEmpty(bulkOp.ColumnName) && bulkOp.Updates != null)
                    {
                        foreach (var update in bulkOp.Updates)
                        {
                            if (update.RowIndex >= 0 && update.RowIndex < data.Count)
                            {
                                data[update.RowIndex].Data[bulkOp.ColumnName] = update.Value ?? "";
                            }
                        }
                    }
                    break;

                case "transform_column":
                    // Apply a transformation formula to a column (values pre-computed)
                    if (!string.IsNullOrEmpty(bulkOp.ColumnName) && bulkOp.TransformedValues != null)
                    {
                        for (int i = 0; i < Math.Min(data.Count, bulkOp.TransformedValues.Count); i++)
                        {
                            data[i].Data[bulkOp.ColumnName] = bulkOp.TransformedValues[i];
                        }
                    }
                    break;

                case "delete_rows":
                    // Delete multiple rows (in reverse order to maintain indices)
                    if (bulkOp.RowIndices != null)
                    {
                        foreach (var rowIndex in bulkOp.RowIndices.OrderByDescending(i => i))
                        {
                            if (rowIndex >= 0 && rowIndex < data.Count)
                            {
                                data.RemoveAt(rowIndex);
                            }
                        }
                    }
                    break;

                case "insert_rows":
                    // Insert multiple rows
                    if (bulkOp.NewRows != null)
                    {
                        foreach (var newRow in bulkOp.NewRows)
                        {
                            data.Add(newRow);
                        }
                    }
                    break;

                default:
                    _logger.LogWarning("Unknown bulk operation type: {Operation}", bulkOp.Operation);
                    break;
            }

            _logger.LogDebug("Applied bulk operation '{Operation}' from event {EventId}", bulkOp.Operation, evt.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying bulk operation from event {EventId}", evt.EventId);
        }
    }

    /// <summary>
    /// Gets the version history for a session.
    /// </summary>
    public async Task<List<VersionInfo>> GetVersionHistoryAsync(Guid sessionId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var snapshots = await dbContext.DataSnapshots
            .Where(s => s.SessionId == sessionId)
            .OrderByDescending(s => s.VersionNumber)
            .ToListAsync();

        var versions = new List<VersionInfo>();

        foreach (var snapshot in snapshots)
        {
            var eventCount = await dbContext.DataChangeEvents
                .CountAsync(e => e.SessionId == sessionId && e.FromVersion == snapshot.VersionNumber);

            versions.Add(new VersionInfo
            {
                VersionNumber = snapshot.VersionNumber,
                CreatedAt = snapshot.CreatedAt,
                RowCount = snapshot.RowCount,
                EventCount = eventCount,
                SnapshotSize = snapshot.CompressedData.Length
            });
        }

        return versions;
    }
}

/// <summary>
/// Information about a specific version of a session.
/// </summary>
public class VersionInfo
{
    public int VersionNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public int RowCount { get; set; }
    public int EventCount { get; set; }
    public long SnapshotSize { get; set; }
}

/// <summary>
/// Represents a bulk operation patch for applying multiple changes at once.
/// </summary>
public class BulkOperationPatch
{
    public string Operation { get; set; } = "";
    public string? ColumnName { get; set; }
    public List<BulkUpdate>? Updates { get; set; }
    public List<string>? TransformedValues { get; set; }
    public List<int>? RowIndices { get; set; }
    public List<GenericCsvRow>? NewRows { get; set; }
}

/// <summary>
/// Represents a single update in a bulk operation.
/// </summary>
public class BulkUpdate
{
    public int RowIndex { get; set; }
    public string? Value { get; set; }
}
