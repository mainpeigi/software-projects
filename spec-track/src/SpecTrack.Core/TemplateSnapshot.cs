namespace SpecTrack.Core;

public record TemplateSnapshot(
    int SchemaVersion,
    string Name,
    string? Description,
    List<SnapCategory> Categories,
    List<SnapComponent> Components,
    List<SnapChecklistItem> ChecklistItems,
    Dictionary<string, bool>? PresetFlags = null
);

public record SnapCategory(
    Guid Key,
    string Name,
    int SortOrder
);

public record SnapComponent(
    Guid Key,
    Guid? CategoryKey,
    string Title,
    string? Description,
    int SortOrder,
    bool IsRequired,
    int Quantity,
    Guid? PartId,
    SnapPart? Part
);

public record SnapPart(
    string Name,
    string? Mpn,
    string? Spn,
    List<string> SpecNames
);

public record SnapChecklistItem(
    Guid Key,
    string Title,
    string? Description,
    int SortOrder,
    bool IsRequired,
    int? DueDateOffsetDays,
    string? DefaultAssigneeRole,
    string? Severity = null
);
