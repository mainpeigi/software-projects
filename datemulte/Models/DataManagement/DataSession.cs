namespace Datemulte_2.Models.DataManagement;

/// <summary>
/// Represents a user session with an uploaded data file and its edit history.
/// Replaces/extends the current SessionState model with database-backed persistence.
/// </summary>
public class DataSession
{
    // Primary key - unique session identifier
    public Guid SessionId { get; set; } = Guid.NewGuid();
    // User-defined session name
    public string SessionName { get; set; } = "Untitled Session";
    // Foreign key to user (Supabase Auth user ID)
    public Guid UserId { get; set; }
    // Session creation timestamp
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // Last modification timestamp
    public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;

    // === File Metadata ===
    // Foreign key to source file entity
    public Guid? SourceFileId { get; set; }
    // Original filename
    public string? SourceFileName { get; set; }
    // File size in bytes
    public long SourceFileSize { get; set; }
    // SHA256 hash of original file for integrity verification
    public string? SourceFileHash { get; set; }

    // === Data Schema (stored as JSON in database) ===
    // All column names in the dataset
    public List<string> Columns { get; set; } = new();
    // Columns containing numeric data
    public List<string> NumericColumns { get; set; } = new();
    // Time/X-axis column name
    public string? TimeColumn { get; set; }
    // Units of measurement for each column
    public Dictionary<string, string> ColumnUnits { get; set; } = new();
    // Maps columns to sensor groups
    public Dictionary<string, string> ColumnToGroupMapping { get; set; } = new();
    // List of sensor group names
    public List<string> SensorGroups { get; set; } = new();

    // === Versioning (Hybrid Snapshot + Event Log Architecture) ===
    // Current version number of the session
    public int CurrentVersion { get; set; } = 1;
    // Number of change events since last snapshot (triggers new snapshot at threshold)
    public int EventCountSinceSnapshot { get; set; } = 0;
    // Timestamp of last snapshot creation
    public DateTime? LastSnapshotAt { get; set; }

    // === Navigation Properties (EF Core relationships) ===
    // Related source file entity
    public SourceFile? SourceFile { get; set; }
    // Collection of data snapshots (periodic compressed backups)
    public ICollection<DataSnapshot> Snapshots { get; set; } = new List<DataSnapshot>();
    // Collection of change events (incremental modifications)
    public ICollection<DataChangeEvent> ChangeEvents { get; set; } = new List<DataChangeEvent>();
}
