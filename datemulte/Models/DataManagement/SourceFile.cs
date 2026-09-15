namespace Datemulte_2.Models.DataManagement;

/// <summary>
/// Represents the raw uploaded file (CSV or Excel) stored in blob storage or file system.
/// </summary>
public class SourceFile
{
    public Guid FileId { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = "";
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = ""; // "text/csv" or "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"

    // Storage configuration
    public string StorageProvider { get; set; } = "FileSystem"; // "AzureBlob" or "FileSystem"
    public string StoragePath { get; set; } = ""; // Blob URL or file system path
    public string FileHash { get; set; } = ""; // SHA256 for deduplication and integrity

    // For Excel files
    public string? SheetName { get; set; }
    public bool IsGroupedFormat { get; set; }

    // Metadata
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public Guid UploadedBy { get; set; }

    // Processing status
    public string ProcessingStatus { get; set; } = "Pending"; // "Pending", "Processing", "Completed", "Failed"
    public string? ErrorMessage { get; set; }
    public Guid? SessionId { get; set; } // Set after successful processing
}
