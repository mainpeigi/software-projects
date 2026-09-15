namespace Datemulte_2.Services.Security;

/// <summary>
/// Validates file uploads for security (MIME type validation, size limits, malicious content detection)
/// </summary>
public class FileUploadValidator
{
    // Magic bytes (file signatures) for supported file types
    private static readonly Dictionary<string, byte[]> MimeSignatures = new()
    {
        { "image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
        { "image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF } },
        { "image/gif", new byte[] { 0x47, 0x49, 0x46, 0x38 } },
        { "text/csv", new byte[] { } }, // CSV has no magic bytes
        { "text/plain", new byte[] { } }, // Plain text has no magic bytes
        { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
          new byte[] { 0x50, 0x4B, 0x03, 0x04 } }, // ZIP header (used by .xlsx)
        { "application/vnd.ms-excel", 
          new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } } // Old Excel .xls
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv", ".txt", ".xlsx", ".xls", ".png", ".jpg", ".jpeg", ".gif"
    };

    /// <summary>
    /// Validates a file upload for security issues
    /// </summary>
    /// <param name="fileStream">The file stream to validate</param>
    /// <param name="fileName">Original filename</param>
    /// <param name="contentType">MIME content type from browser</param>
    /// <param name="maxSizeBytes">Maximum allowed file size in bytes (default: 5MB for images, 100MB for data files)</param>
    /// <returns>Tuple with validation result and error message if invalid</returns>
    public static (bool isValid, string error) ValidateFile(
        Stream fileStream,
        string fileName,
        string contentType,
        long maxSizeBytes = 0)
    {
        // Validate filename
        if (string.IsNullOrWhiteSpace(fileName))
            return (false, "Filename is required");

        // Sanitize and validate file extension
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return (false, $"File type '{extension}' is not allowed. Allowed types: {string.Join(", ", AllowedExtensions)}");

        // Check for path traversal attempts
        if (fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
            return (false, "Filename contains invalid characters");

        // Set default max size based on file type
        if (maxSizeBytes == 0)
        {
            maxSizeBytes = IsImageFile(extension) 
                ? 5 * 1024 * 1024      // 5MB for images
                : 100 * 1024 * 1024;   // 100MB for data files
        }

        // Validate file size
        if (fileStream.Length > maxSizeBytes)
        {
            var maxMB = maxSizeBytes / (1024.0 * 1024.0);
            return (false, $"File size ({fileStream.Length / (1024.0 * 1024.0):F2}MB) exceeds maximum allowed size ({maxMB:F2}MB)");
        }

        if (fileStream.Length == 0)
            return (false, "File is empty");

        // Validate MIME type by checking magic bytes
        if (MimeSignatures.TryGetValue(contentType, out var signature))
        {
            if (signature.Length > 0)
            {
                byte[] buffer = new byte[signature.Length];
                int bytesRead = fileStream.Read(buffer, 0, signature.Length);
                fileStream.Position = 0; // Reset stream position

                if (bytesRead < signature.Length)
                    return (false, "File is too small to validate");

                if (!buffer.Take(signature.Length).SequenceEqual(signature))
                    return (false, $"File content does not match declared type ({contentType})");
            }
        }
        else
        {
            return (false, $"Content type '{contentType}' is not supported");
        }

        return (true, "");
    }

    /// <summary>
    /// Sanitizes a filename by removing potentially dangerous characters
    /// </summary>
    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "file";

        // Remove path separators and special characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

        // Remove path traversal attempts
        sanitized = sanitized.Replace("..", "");

        // Limit length
        if (sanitized.Length > 255)
            sanitized = sanitized.Substring(0, 255);

        return sanitized;
    }

    private static bool IsImageFile(string extension)
    {
        return extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".gif";
    }
}
