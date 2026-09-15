namespace Datemulte_2.Models.DataManagement;

/// <summary>
/// Represents a delta edit event (cell update, row insert/delete, bulk transform, etc.).
/// Events are applied on top of snapshots to reconstruct the current state.
/// </summary>
public class DataChangeEvent
{
    public long EventId { get; set; }
    public Guid SessionId { get; set; }
    public int FromVersion { get; set; } // Snapshot version this event applies to
    public int EventSequence { get; set; } // Order within version (1, 2, 3, ...)
    public DataEventType EventType { get; set; }

    // For single-cell/row operations
    public int? RowIndex { get; set; }
    public string? ColumnName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    // For complex operations (bulk updates, filters, transformations)
    public string? JsonPatch { get; set; } // RFC 6902 JSON Patch or custom operation JSON

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; }

    // Navigation
    public DataSession Session { get; set; } = null!;
}

/// <summary>
/// Types of data change events
/// </summary>
public enum DataEventType
{
    CellUpdate,       // Single cell edit
    RowInsert,        // Insert new row
    RowDelete,        // Delete row
    BulkUpdate,       // Update multiple cells
    ColumnTransform,  // Apply formula/transformation to entire column
    Filter,           // Apply filter (marks rows as hidden)
    Sort              // Reorder rows
}
