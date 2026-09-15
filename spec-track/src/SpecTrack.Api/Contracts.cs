using SpecTrack.Core;

namespace SpecTrack.Api;

public record SaveSnapshotRequest(string Label, TemplateSnapshot Snapshot);
public record CompareRequest(Guid FromId, Guid ToId);
public record PreviewRequest(TemplateSnapshot From, TemplateSnapshot To);
public record SnapshotRecord(Guid Id, string Label, DateTimeOffset CreatedAt, TemplateSnapshot Snapshot)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public SnapshotInfo Info => new(Id, Label, CreatedAt, Snapshot.Name,
        Snapshot.Components?.Count ?? 0, Snapshot.ChecklistItems?.Count ?? 0);
}
public record SnapshotInfo(Guid Id, string Label, DateTimeOffset CreatedAt, string Name,
    int ComponentCount, int ChecklistCount);
public record ComparisonSummary(int Added, int Removed, int Changed, int Reordered,
    int FieldChanges, bool FlagsChanged, bool IsEmpty)
{
    public static ComparisonSummary From(TemplateDiffResult diff) => new(
        diff.Categories.Added.Count + diff.Components.Added.Count + diff.ChecklistItems.Added.Count,
        diff.Categories.Removed.Count + diff.Components.Removed.Count + diff.ChecklistItems.Removed.Count,
        diff.Categories.Changed.Count + diff.Components.Changed.Count + diff.ChecklistItems.Changed.Count,
        diff.Categories.Changed.Count(x => x.ReorderOnly) + diff.Components.Changed.Count(x => x.ReorderOnly)
            + diff.ChecklistItems.Changed.Count(x => x.ReorderOnly),
        diff.FieldChanges.Count, diff.PresetFlagsChanged, diff.IsEmpty);
}
