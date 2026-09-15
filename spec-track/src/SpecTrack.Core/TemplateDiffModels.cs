namespace SpecTrack.Core;

public record DiffSection<T>(
    List<T> Added,
    List<T> Removed,
    List<T> Changed
);

public record FieldChangeDto(
    string Field,
    string? Before,
    string? After
);

public record CategoryDiffDto
{
    public Guid Key { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? RenamedFrom { get; init; }
    public int? SortOrderBefore { get; init; }
    public int? SortOrderAfter { get; init; }
    public bool ReorderOnly { get; init; }
}

public record ComponentDiffDto
{
    public Guid Key { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? CategoryName { get; init; }
    public string? PartName { get; init; }
    public string? PartMpn { get; init; }
    public int Quantity { get; init; }
    public bool IsRequired { get; init; }
    public List<FieldChangeDto> Fields { get; init; } = new();
    public List<string> SpecsAdded { get; init; } = new();
    public List<string> SpecsRemoved { get; init; } = new();
    public bool ReorderOnly { get; init; }
}

public record ChecklistItemDiffDto
{
    public Guid Key { get; init; }
    public string Title { get; init; } = string.Empty;
    public List<FieldChangeDto> Fields { get; init; } = new();
    public bool ReorderOnly { get; init; }
}
