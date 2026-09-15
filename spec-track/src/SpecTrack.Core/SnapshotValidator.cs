namespace SpecTrack.Core;

/// <summary>Checks the standalone snapshot contract before storage or comparison.</summary>
public static class SnapshotValidator
{
    private const int MaxRows = 5_000;
    private const int MaxShortTextLength = 200;
    private const int MaxDescriptionLength = 4_000;
    private const int MaxFlags = 256;
    private const int MaxSpecsPerPart = 256;
    private const int MaxTotalSpecs = 20_000;

    public static void Validate(TemplateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.SchemaVersion != 1)
            throw Invalid("SchemaVersion must be 1.");

        RequiredText(snapshot.Name, "Name");
        OptionalText(snapshot.Description, "Description", MaxDescriptionLength);

        var rowCount = (long)(snapshot.Categories?.Count ?? 0)
            + (snapshot.Components?.Count ?? 0) + (snapshot.ChecklistItems?.Count ?? 0);
        if (rowCount > MaxRows)
            throw Invalid($"A snapshot may contain at most {MaxRows} total rows.");

        var categoryKeys = ValidateKeys(snapshot.Categories, c => c.Key, "Categories");
        ValidateKeys(snapshot.Components, c => c.Key, "Components");
        ValidateKeys(snapshot.ChecklistItems, c => c.Key, "ChecklistItems");

        foreach (var category in snapshot.Categories ?? [])
            RequiredText(category.Name, "Category.Name");

        var totalSpecs = 0;
        foreach (var component in snapshot.Components ?? [])
        {
            RequiredText(component.Title, "Component.Title");
            OptionalText(component.Description, "Component.Description", MaxDescriptionLength);
            if (component.CategoryKey is Guid categoryKey && !categoryKeys.Contains(categoryKey))
                throw Invalid("Component.CategoryKey must reference a category in the same snapshot.");
            if (component.Quantity is < 1 or > 1_000_000)
                throw Invalid("Component.Quantity must be between 1 and 1000000.");
            if (component.PartId == Guid.Empty)
                throw Invalid("Component.PartId must be a nonempty GUID when supplied.");

            if (component.Part is not { } part) continue;
            RequiredText(part.Name, "Part.Name");
            OptionalText(part.Mpn, "Part.Mpn");
            OptionalText(part.Spn, "Part.Spn");
            var specCount = part.SpecNames?.Count ?? 0;
            if (specCount > MaxSpecsPerPart)
                throw Invalid($"A part may contain at most {MaxSpecsPerPart} specifications.");
            totalSpecs += specCount;
            if (totalSpecs > MaxTotalSpecs)
                throw Invalid($"A snapshot may contain at most {MaxTotalSpecs} total specifications.");
            foreach (var spec in part.SpecNames ?? [])
            {
                if (spec is null)
                    throw Invalid("Part.SpecNames must not contain null entries.");
                // Blank names remain valid and are ignored by the source normalization rules.
                OptionalText(spec, "Part.SpecNames entry");
            }
        }

        foreach (var item in snapshot.ChecklistItems ?? [])
        {
            RequiredText(item.Title, "ChecklistItem.Title");
            OptionalText(item.Description, "ChecklistItem.Description", MaxDescriptionLength);
            OptionalText(item.DefaultAssigneeRole, "ChecklistItem.DefaultAssigneeRole");
            OptionalText(item.Severity, "ChecklistItem.Severity");
        }

        if (snapshot.PresetFlags is not { } flags) return;
        if (flags.Count > MaxFlags)
            throw Invalid($"A snapshot may contain at most {MaxFlags} preset flags.");
        foreach (var name in flags.Keys)
            RequiredText(name, "PresetFlags key");
    }

    private static HashSet<Guid> ValidateKeys<T>(List<T>? rows, Func<T, Guid> getKey, string section)
        where T : class
    {
        var keys = new HashSet<Guid>();
        foreach (var row in rows ?? [])
        {
            if (row is null)
                throw Invalid($"{section} must not contain null entries.");
            var key = getKey(row);
            if (key == Guid.Empty)
                throw Invalid($"{section} row keys must be nonempty GUIDs.");
            if (!keys.Add(key))
                throw Invalid($"{section} row keys must be unique within their section.");
        }
        return keys;
    }

    private static void RequiredText(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw Invalid($"{field} must not be blank.");
        OptionalText(value, field);
    }

    private static void OptionalText(string? value, string field, int maxLength = MaxShortTextLength)
    {
        if (value?.Length > maxLength)
            throw Invalid($"{field} must be at most {maxLength} characters.");
    }

    private static ArgumentException Invalid(string message) => new(message, "snapshot");
}
