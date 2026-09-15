using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Datemulte_2.Services.DataManagement;

/// <summary>
/// Utilities for data compression and hashing.
/// Used for snapshot compression and file integrity verification.
/// </summary>
public static class CompressionUtilities
{
    /// <summary>
    /// Compresses a string using Gzip.
    /// </summary>
    public static byte[] CompressGzip(string text)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<byte>();

        var bytes = Encoding.UTF8.GetBytes(text);
        return CompressGzip(bytes);
    }

    /// <summary>
    /// Compresses a byte array using Gzip.
    /// </summary>
    public static byte[] CompressGzip(byte[] data)
    {
        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal))
        {
            gzipStream.Write(data, 0, data.Length);
        }
        return outputStream.ToArray();
    }

    /// <summary>
    /// Decompresses Gzip data to a string.
    /// </summary>
    public static string DecompressGzipToString(byte[] compressedData)
    {
        if (compressedData == null || compressedData.Length == 0)
            return string.Empty;

        var decompressed = DecompressGzip(compressedData);
        return Encoding.UTF8.GetString(decompressed);
    }

    /// <summary>
    /// Decompresses Gzip data to a byte array.
    /// </summary>
    public static byte[] DecompressGzip(byte[] compressedData)
    {
        using var inputStream = new MemoryStream(compressedData);
        using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();

        gzipStream.CopyTo(outputStream);
        return outputStream.ToArray();
    }

    /// <summary>
    /// Computes SHA256 hash of data.
    /// </summary>
    public static byte[] ComputeSHA256(byte[] data)
    {
        return SHA256.HashData(data);
    }

    /// <summary>
    /// Computes SHA256 hash of a string.
    /// </summary>
    public static byte[] ComputeSHA256(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return SHA256.HashData(bytes);
    }

    /// <summary>
    /// Computes SHA256 hash and returns as hex string.
    /// </summary>
    public static string ComputeSHA256Hex(byte[] data)
    {
        var hash = ComputeSHA256(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Computes SHA256 hash of a stream (useful for large files).
    /// </summary>
    public static async Task<byte[]> ComputeSHA256Async(Stream stream)
    {
        return await SHA256.HashDataAsync(stream);
    }

    /// <summary>
    /// Verifies data integrity using SHA256 checksum.
    /// </summary>
    public static bool VerifyChecksum(byte[] data, byte[] expectedChecksum)
    {
        var actualChecksum = ComputeSHA256(data);
        return actualChecksum.SequenceEqual(expectedChecksum);
    }

    /// <summary>
    /// Gets compression ratio as a percentage.
    /// </summary>
    public static double GetCompressionRatio(long originalSize, long compressedSize)
    {
        if (originalSize == 0)
            return 0;

        return (1.0 - ((double)compressedSize / originalSize)) * 100.0;
    }
}
