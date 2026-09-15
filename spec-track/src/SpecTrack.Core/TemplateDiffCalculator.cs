namespace SpecTrack.Core;

public record TemplateDiffResult(
    List<FieldChangeDto> FieldChanges,
    DiffSection<CategoryDiffDto> Categories,
    DiffSection<ComponentDiffDto> Components,
    DiffSection<ChecklistItemDiffDto> ChecklistItems,
    bool PresetFlagsChanged = false)
{
    public bool IsEmpty =>
        FieldChanges.Count == 0
        && !PresetFlagsChanged
        && Categories.Added.Count == 0 && Categories.Removed.Count == 0 && Categories.Changed.Count == 0
        && Components.Added.Count == 0 && Components.Removed.Count == 0 && Components.Changed.Count == 0
        && ChecklistItems.Added.Count == 0 && ChecklistItems.Removed.Count == 0 && ChecklistItems.Changed.Count == 0;
}

public static class TemplateDiffCalculator
{
    public static TemplateDiffResult Diff(TemplateSnapshot baseSnapshot, TemplateSnapshot target)
    {
        SnapshotValidator.Validate(baseSnapshot);
        SnapshotValidator.Validate(target);
        var b = Normalize(baseSnapshot);
        var t = Normalize(target);

        var fieldChanges = DiffFields(b, t);
        var baseCategoryNames = b.Categories.ToDictionary(c => c.Key, c => c.Name);
        var targetCategoryNames = t.Categories.ToDictionary(c => c.Key, c => c.Name);

        return new TemplateDiffResult(
            fieldChanges,
            DiffCategories(b.Categories, t.Categories),
            DiffComponents(b.Components, t.Components, baseCategoryNames, targetCategoryNames),
            DiffChecklistItems(b.ChecklistItems, t.ChecklistItems),
            !FlagsEqual(b.PresetFlags, t.PresetFlags));
    }

    private static List<FieldChangeDto> DiffFields(TemplateSnapshot b, TemplateSnapshot t)
    {
        var fields = new List<FieldChangeDto>();
        if (!string.Equals(b.Name, t.Name, StringComparison.Ordinal))
            fields.Add(new FieldChangeDto("Name", b.Name, t.Name));
        if (!string.Equals(b.Description, t.Description, StringComparison.Ordinal))
            fields.Add(new FieldChangeDto("Description", b.Description, t.Description));
        return fields;
    }

    private static DiffSection<CategoryDiffDto> DiffCategories(List<SnapCategory> baseCats, List<SnapCategory> targetCats)
    {
        var baseByKey = ByKey(baseCats, c => c.Key);
        var targetByKey = ByKey(targetCats, c => c.Key);

        var added = new List<CategoryDiffDto>();
        var removed = new List<CategoryDiffDto>();
        var changed = new List<CategoryDiffDto>();

        foreach (var cat in targetCats)
        {
            if (!baseByKey.TryGetValue(cat.Key, out var old))
            {
                added.Add(new CategoryDiffDto { Key = cat.Key, Name = cat.Name });
                continue;
            }

            var renamed = !string.Equals(old.Name, cat.Name, StringComparison.Ordinal);
            var reordered = old.SortOrder != cat.SortOrder;
            if (!renamed && !reordered) continue;

            changed.Add(new CategoryDiffDto
            {
                Key = cat.Key,
                Name = cat.Name,
                RenamedFrom = renamed ? old.Name : null,
                SortOrderBefore = reordered ? old.SortOrder : null,
                SortOrderAfter = reordered ? cat.SortOrder : null,
                ReorderOnly = !renamed
            });
        }
        foreach (var cat in baseCats)
        {
            if (!targetByKey.ContainsKey(cat.Key))
                removed.Add(new CategoryDiffDto { Key = cat.Key, Name = cat.Name });
        }

        return new DiffSection<CategoryDiffDto>(added, removed, changed);
    }

    private static DiffSection<ComponentDiffDto> DiffComponents(
        List<SnapComponent> baseComponents, List<SnapComponent> targetComponents,
        Dictionary<Guid, string> baseCategoryNames, Dictionary<Guid, string> targetCategoryNames)
    {
        var baseByKey = ByKey(baseComponents, c => c.Key);
        var targetByKey = ByKey(targetComponents, c => c.Key);

        var added = new List<ComponentDiffDto>();
        var removed = new List<ComponentDiffDto>();
        var changed = new List<ComponentDiffDto>();

        foreach (var tc in targetComponents)
        {
            if (!baseByKey.TryGetValue(tc.Key, out var bc))
            {
                added.Add(ToComponentDto(tc, targetCategoryNames, new(), tc.Part?.SpecNames ?? new(), new(), false));
                continue;
            }

            var fields = new List<FieldChangeDto>();
            if (!string.Equals(bc.Title, tc.Title, StringComparison.Ordinal))
                fields.Add(new FieldChangeDto("Title", bc.Title, tc.Title));
            if (!string.Equals(bc.Description, tc.Description, StringComparison.Ordinal))
                fields.Add(new FieldChangeDto("Description", bc.Description, tc.Description));
            if (bc.IsRequired != tc.IsRequired)
                fields.Add(new FieldChangeDto("Required", RenderRequired(bc.IsRequired), RenderRequired(tc.IsRequired)));
            if (bc.Quantity != tc.Quantity)
                fields.Add(new FieldChangeDto("Quantity", bc.Quantity.ToString(), tc.Quantity.ToString()));
            if (bc.PartId != tc.PartId)
                fields.Add(new FieldChangeDto("Part", RenderPart(bc), RenderPart(tc)));
            else
            {
                if (!string.Equals(bc.Part?.Name, tc.Part?.Name, StringComparison.Ordinal))
                    fields.Add(new FieldChangeDto("PartName", bc.Part?.Name, tc.Part?.Name));
                if (!string.Equals(bc.Part?.Mpn, tc.Part?.Mpn, StringComparison.Ordinal))
                    fields.Add(new FieldChangeDto("PartMpn", bc.Part?.Mpn, tc.Part?.Mpn));
                if (!string.Equals(bc.Part?.Spn, tc.Part?.Spn, StringComparison.Ordinal))
                    fields.Add(new FieldChangeDto("PartSpn", bc.Part?.Spn, tc.Part?.Spn));
            }
            if (bc.CategoryKey != tc.CategoryKey)
                fields.Add(new FieldChangeDto("Category",
                    CategoryName(bc.CategoryKey, baseCategoryNames) ?? "Uncategorized",
                    CategoryName(tc.CategoryKey, targetCategoryNames) ?? "Uncategorized"));

            var baseSpecs = bc.Part?.SpecNames ?? new List<string>();
            var targetSpecs = tc.Part?.SpecNames ?? new List<string>();
            var baseSpecSet = new HashSet<string>(baseSpecs, StringComparer.OrdinalIgnoreCase);
            var targetSpecSet = new HashSet<string>(targetSpecs, StringComparer.OrdinalIgnoreCase);
            var specsAdded = targetSpecs.Where(n => !baseSpecSet.Contains(n)).ToList();
            var specsRemoved = baseSpecs.Where(n => !targetSpecSet.Contains(n)).ToList();

            var sortChanged = bc.SortOrder != tc.SortOrder;
            if (fields.Count == 0 && specsAdded.Count == 0 && specsRemoved.Count == 0 && !sortChanged)
                continue;

            var reorderOnly = fields.Count == 0 && specsAdded.Count == 0 && specsRemoved.Count == 0;
            changed.Add(ToComponentDto(tc, targetCategoryNames, fields, specsAdded, specsRemoved, reorderOnly));
        }

        foreach (var bc in baseComponents)
        {
            if (!targetByKey.ContainsKey(bc.Key))
                removed.Add(ToComponentDto(bc, baseCategoryNames, new(), new(), new(), false));
        }

        return new DiffSection<ComponentDiffDto>(added, removed, changed);
    }

    private static DiffSection<ChecklistItemDiffDto> DiffChecklistItems(
        List<SnapChecklistItem> baseItems, List<SnapChecklistItem> targetItems)
    {
        var baseByKey = ByKey(baseItems, i => i.Key);
        var targetByKey = ByKey(targetItems, i => i.Key);

        var added = new List<ChecklistItemDiffDto>();
        var removed = new List<ChecklistItemDiffDto>();
        var changed = new List<ChecklistItemDiffDto>();

        foreach (var ti in targetItems)
        {
            if (!baseByKey.TryGetValue(ti.Key, out var bi))
            {
                added.Add(new ChecklistItemDiffDto { Key = ti.Key, Title = ti.Title });
                continue;
            }

            var fields = new List<FieldChangeDto>();
            if (!string.Equals(bi.Title, ti.Title, StringComparison.Ordinal))
                fields.Add(new FieldChangeDto("Title", bi.Title, ti.Title));
            if (!string.Equals(bi.Description, ti.Description, StringComparison.Ordinal))
                fields.Add(new FieldChangeDto("Description", bi.Description, ti.Description));
            if (bi.IsRequired != ti.IsRequired)
                fields.Add(new FieldChangeDto("Required", RenderRequired(bi.IsRequired), RenderRequired(ti.IsRequired)));
            if (bi.DueDateOffsetDays != ti.DueDateOffsetDays)
                fields.Add(new FieldChangeDto("DueOffset",
                    bi.DueDateOffsetDays?.ToString() ?? string.Empty,
                    ti.DueDateOffsetDays?.ToString() ?? string.Empty));
            if (!string.Equals(bi.DefaultAssigneeRole, ti.DefaultAssigneeRole, StringComparison.Ordinal))
                fields.Add(new FieldChangeDto("AssigneeRole", bi.DefaultAssigneeRole, ti.DefaultAssigneeRole));
            if (!string.Equals(bi.Severity, ti.Severity, StringComparison.Ordinal))
                fields.Add(new FieldChangeDto("Severity", bi.Severity, ti.Severity));

            var sortChanged = bi.SortOrder != ti.SortOrder;
            if (fields.Count == 0 && !sortChanged)
                continue;

            changed.Add(new ChecklistItemDiffDto
            {
                Key = ti.Key,
                Title = ti.Title,
                Fields = fields,
                ReorderOnly = fields.Count == 0
            });
        }

        foreach (var bi in baseItems)
        {
            if (!targetByKey.ContainsKey(bi.Key))
                removed.Add(new ChecklistItemDiffDto { Key = bi.Key, Title = bi.Title });
        }

        return new DiffSection<ChecklistItemDiffDto>(added, removed, changed);
    }

    private static ComponentDiffDto ToComponentDto(
        SnapComponent c, Dictionary<Guid, string> categoryNames,
        List<FieldChangeDto> fields, List<string> specsAdded, List<string> specsRemoved, bool reorderOnly) =>
        new()
        {
            Key = c.Key,
            Title = c.Title,
            CategoryName = CategoryName(c.CategoryKey, categoryNames),
            PartName = c.Part?.Name,
            PartMpn = c.Part?.Mpn,
            Quantity = c.Quantity,
            IsRequired = c.IsRequired,
            Fields = fields,
            SpecsAdded = specsAdded,
            SpecsRemoved = specsRemoved,
            ReorderOnly = reorderOnly
        };

    private static string? CategoryName(Guid? key, Dictionary<Guid, string> categoryNames) =>
        key is Guid k && categoryNames.TryGetValue(k, out var name) ? name : null;

    private static string RenderRequired(bool isRequired) => isRequired ? "required" : "optional";

    private static string? RenderPart(SnapComponent c)
    {
        if (c.PartId is null) return null;
        if (c.Part is null) return c.PartId.Value.ToString();
        return string.IsNullOrEmpty(c.Part.Spn) ? c.Part.Name : $"{c.Part.Name} ({c.Part.Spn})";
    }

    private static Dictionary<Guid, T> ByKey<T>(List<T> items, Func<T, Guid> keySelector) =>
        items.ToDictionary(keySelector);

    private static TemplateSnapshot Normalize(TemplateSnapshot s) =>
        new(
            s.SchemaVersion,
            Clean(s.Name),
            CleanOrNull(s.Description),
            (s.Categories ?? new()).Select(c => c with { Name = Clean(c.Name) }).ToList(),
            (s.Components ?? new()).Select(c => c with
            {
                Title = Clean(c.Title),
                Description = CleanOrNull(c.Description),
                Part = c.Part is null ? null : new SnapPart(
                    Clean(c.Part.Name),
                    CleanOrNull(c.Part.Mpn),
                    CleanOrNull(c.Part.Spn),
                    (c.Part.SpecNames ?? new()).Select(Clean).Where(n => n.Length > 0).ToList())
            }).ToList(),
            (s.ChecklistItems ?? new()).Select(i => i with
            {
                Title = Clean(i.Title),
                Description = CleanOrNull(i.Description),
                DefaultAssigneeRole = CleanOrNull(i.DefaultAssigneeRole)
            }).ToList(),
            s.PresetFlags);

    private static bool FlagsEqual(Dictionary<string, bool>? a, Dictionary<string, bool>? b)
    {
        a ??= new();
        b ??= new();
        if (a.Count != b.Count) return false;
        foreach (var kv in a)
            if (!b.TryGetValue(kv.Key, out var v) || v != kv.Value) return false;
        return true;
    }

    private static string Clean(string? value) => value?.Trim() ?? string.Empty;

    private static string? CleanOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
