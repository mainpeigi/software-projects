namespace Datemulte_2.Models.DataManagement;

/// <summary>
/// Represents a compressed snapshot of the entire dataset at a specific version.
/// Snapshots are created periodically (e.g., every 50 edits) to optimize session resume performance.
/// </summary>
public class DataSnapshot
{
    // Primary key - auto-incrementing snapshot ID
    public long SnapshotId { get; set; }
    // Foreign key to data session
    public Guid SessionId { get; set; }
    // Version number this snapshot represents
    public int VersionNumber { get; set; }
    // Number of rows in the dataset at this version
    public int RowCount { get; set; }

    // === Compressed Data Storage ===
    // Gzip compressed JSON data (optimized for storage)
    public byte[] CompressedData { get; set; } = Array.Empty<byte>();
    // Original uncompressed size in bytes
    public long UncompressedSize { get; set; }

    // === Integrity Verification ===
    // SHA256 checksum for data integrity verification
    public byte[] Checksum { get; set; } = Array.Empty<byte>();

    // Snapshot creation timestamp
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // === Navigation Property ===
    // Parent data session
    public DataSession Session { get; set; } = null!;
}
