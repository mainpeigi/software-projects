using System.Text.Json;
using SpecTrack.Core;

namespace SpecTrack.Api;

/// <summary>Immutable snapshots stored independently using atomic file replacement.</summary>
public sealed class FileSnapshotStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _snapshotsDirectory;
    public string DataDirectory { get; }

    public FileSnapshotStore(IConfiguration configuration, IHostEnvironment environment)
    {
        DataDirectory = Path.GetFullPath(configuration["Storage:Directory"] ?? "storage", environment.ContentRootPath);
        _snapshotsDirectory = Path.Combine(DataDirectory, "snapshots");
        Directory.CreateDirectory(_snapshotsDirectory);
    }

    public async Task<SnapshotRecord> SaveAsync(string label, TemplateSnapshot snapshot, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(label) || label.Length > 120)
            throw new ArgumentException("Label must contain between 1 and 120 characters.");
        SnapshotValidator.Validate(snapshot);
        // Detach the caller's mutable lists before persisting or returning the record.
        var copy = JsonSerializer.Deserialize<TemplateSnapshot>(JsonSerializer.Serialize(snapshot, Json), Json)!;
        copy = copy with
        {
            Name = copy.Name.Trim(),
            Categories = copy.Categories ?? [],
            Components = (copy.Components ?? []).Select(component => component.Part is { SpecNames: null } part
                ? component with { Part = part with { SpecNames = [] } } : component).ToList(),
            ChecklistItems = copy.ChecklistItems ?? []
        };
        var record = new SnapshotRecord(Guid.NewGuid(), label.Trim(), DateTimeOffset.UtcNow, copy);
        var destination = GetPath(record.Id);
        var temporary = Path.Combine(_snapshotsDirectory, $".{record.Id:N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, record, Json, ct);
                await stream.FlushAsync(ct);
            }
            File.Move(temporary, destination, overwrite: false);
            return record;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public async Task<SnapshotRecord?> GetAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            await using var stream = new FileStream(GetPath(id), FileMode.Open, FileAccess.Read,
                FileShare.Read, 65536, FileOptions.Asynchronous);
            var record = await JsonSerializer.DeserializeAsync<SnapshotRecord>(stream, Json, ct);
            if (record is null || record.Id != id || string.IsNullOrWhiteSpace(record.Label))
                throw new InvalidDataException("The saved snapshot is invalid.");
            SnapshotValidator.Validate(record.Snapshot);
            return record;
        }
        catch (FileNotFoundException) { return null; }
        catch (JsonException ex) { throw new InvalidDataException("The saved snapshot is unreadable.", ex); }
        catch (ArgumentException ex) { throw new InvalidDataException("The saved snapshot is invalid.", ex); }
    }

    public async Task<IReadOnlyList<SnapshotInfo>> ListAsync(int limit = 100, CancellationToken ct = default)
    {
        if (limit is < 1 or > 500) throw new ArgumentException("Limit must be between 1 and 500.");
        var result = new List<SnapshotInfo>();
        var files = new DirectoryInfo(_snapshotsDirectory).EnumerateFiles("*.json")
            .Where(file => Guid.TryParseExact(Path.GetFileNameWithoutExtension(file.Name), "N", out _))
            .OrderByDescending(file => file.LastWriteTimeUtc).Take(limit);
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var record = await GetAsync(Guid.ParseExact(Path.GetFileNameWithoutExtension(file.Name), "N"), ct);
            if (record is not null) result.Add(record.Info);
        }
        return result.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).ToArray();
    }

    private string GetPath(Guid id) => Path.Combine(_snapshotsDirectory, $"{id:N}.json");
}
