using SpecTrack.Core;
using Xunit;

namespace SpecTrack.Core.Tests;

public class TemplateDiffCalculatorTests
{
    [Fact]
    public void ReportsTopLevelFieldChanges()
    {
        var before = Fixtures.Snapshot();
        var after = before with { Name = "Updated project", Description = "New description" };

        var result = TemplateDiffCalculator.Diff(before, after);

        Assert.Equal(
            [new FieldChangeDto("Name", "School project", "Updated project"),
             new FieldChangeDto("Description", null, "New description")], result.FieldChanges);
        Assert.False(result.IsEmpty);
    }

    [Fact]
    public void ReportsAddedRemovedAndRenamedCategoriesByStableKey()
    {
        var before = Fixtures.Snapshot() with
        {
            Categories = [new(Fixtures.CategoryKey, "Electronics", 0), new(Fixtures.RemovedKey, "Retired", 1)]
        };
        var after = before with
        {
            Categories = [new(Fixtures.CategoryKey, "Sensors", 2), new(Fixtures.AddedKey, "Mechanical", 1)]
        };

        var result = TemplateDiffCalculator.Diff(before, after).Categories;

        Assert.Equal("Mechanical", Assert.Single(result.Added).Name);
        Assert.Equal(Fixtures.RemovedKey, Assert.Single(result.Removed).Key);
        var changed = Assert.Single(result.Changed);
        Assert.Equal("Electronics", changed.RenamedFrom);
        Assert.Equal("Sensors", changed.Name);
        Assert.Equal(0, changed.SortOrderBefore);
        Assert.Equal(2, changed.SortOrderAfter);
        Assert.False(changed.ReorderOnly);
    }

    [Fact]
    public void ReportsAddedAndRemovedComponentsWithCategoryAndPartDetails()
    {
        var before = Fixtures.Snapshot();
        var after = before with { Components = [before.Components[0] with { Key = Fixtures.AddedKey, Title = "Humidity sensor" }] };

        var result = TemplateDiffCalculator.Diff(before, after).Components;

        var added = Assert.Single(result.Added);
        Assert.Equal(Fixtures.AddedKey, added.Key);
        Assert.Equal("Humidity sensor", added.Title);
        Assert.Equal("Electronics", added.CategoryName);
        Assert.Equal("Sensor", added.PartName);
        Assert.Equal("S-100", added.PartMpn);
        Assert.Equal(["Voltage", "Accuracy"], added.SpecsAdded);
        Assert.Equal(Fixtures.ComponentKey, Assert.Single(result.Removed).Key);
        Assert.Empty(result.Changed);
    }

    [Fact]
    public void ReportsComponentContentCategoryQuantityAndPartChanges()
    {
        var before = Fixtures.Snapshot();
        var after = before with
        {
            Components = [before.Components[0] with
            {
                Title = "Updated sensor", Description = "With cable", IsRequired = false,
                Quantity = 3, CategoryKey = null, PartId = Fixtures.AddedKey,
                Part = new("Replacement", "S-200", "P-200", ["Voltage", "Range"])
            }]
        };

        var changed = Assert.Single(TemplateDiffCalculator.Diff(before, after).Components.Changed);

        Assert.Equal([
            new FieldChangeDto("Title", "Temperature sensor", "Updated sensor"),
            new FieldChangeDto("Description", null, "With cable"),
            new FieldChangeDto("Required", "required", "optional"),
            new FieldChangeDto("Quantity", "1", "3"),
            new FieldChangeDto("Part", "Sensor (P-100)", "Replacement (P-200)"),
            new FieldChangeDto("Category", "Electronics", "Uncategorized")], changed.Fields);
        Assert.Equal(["Range"], changed.SpecsAdded);
        Assert.Equal(["Accuracy"], changed.SpecsRemoved);
        Assert.False(changed.ReorderOnly);
    }

    [Fact]
    public void ReportsPartMetadataChangesWhenPartIdStaysTheSame()
    {
        var before = Fixtures.Snapshot();
        var after = before with
        {
            Components = [before.Components[0] with
            {
                Part = before.Components[0].Part! with { Name = "Renamed sensor", Mpn = "S-101", Spn = "P-101" }
            }]
        };

        var changed = Assert.Single(TemplateDiffCalculator.Diff(before, after).Components.Changed);

        Assert.Equal([
            new FieldChangeDto("PartName", "Sensor", "Renamed sensor"),
            new FieldChangeDto("PartMpn", "S-100", "S-101"),
            new FieldChangeDto("PartSpn", "P-100", "P-101")], changed.Fields);
        Assert.False(changed.ReorderOnly);
    }

    [Fact]
    public void ReportsPartMetadataRemovalEvenWithoutSpecifications()
    {
        var before = Fixtures.Snapshot();
        before = before with { Components = [before.Components[0] with { Part = new("Sensor", null, null, []) }] };
        var after = before with { Components = [before.Components[0] with { Part = null }] };

        var changed = Assert.Single(TemplateDiffCalculator.Diff(before, after).Components.Changed);

        Assert.Equal(new FieldChangeDto("PartName", "Sensor", null), Assert.Single(changed.Fields));
    }

    [Fact]
    public void ReportsChecklistAddedRemovedAndChangedContent()
    {
        var before = Fixtures.Snapshot();
        before = before with { ChecklistItems = [before.ChecklistItems[0], before.ChecklistItems[0] with { Key = Fixtures.RemovedKey }] };
        var after = before with
        {
            ChecklistItems = [before.ChecklistItems[0] with
            {
                Title = "Review report", Description = "Check references", IsRequired = false,
                DueDateOffsetDays = 5, DefaultAssigneeRole = "Reviewer", Severity = "high"
            }, before.ChecklistItems[0] with { Key = Fixtures.AddedKey, Title = "Submit report" }]
        };

        var result = TemplateDiffCalculator.Diff(before, after).ChecklistItems;

        Assert.Equal("Submit report", Assert.Single(result.Added).Title);
        Assert.Equal(Fixtures.RemovedKey, Assert.Single(result.Removed).Key);
        Assert.Equal([
            new FieldChangeDto("Title", "Write report", "Review report"),
            new FieldChangeDto("Description", null, "Check references"),
            new FieldChangeDto("Required", "required", "optional"),
            new FieldChangeDto("DueOffset", "", "5"),
            new FieldChangeDto("AssigneeRole", null, "Reviewer"),
            new FieldChangeDto("Severity", null, "high")], Assert.Single(result.Changed).Fields);
    }

    [Fact]
    public void DistinguishesReorderOnlyAcrossAllSections()
    {
        var before = Fixtures.Snapshot();
        var after = before with
        {
            Categories = [before.Categories[0] with { SortOrder = 9 }],
            Components = [before.Components[0] with { SortOrder = 9 }],
            ChecklistItems = [before.ChecklistItems[0] with { SortOrder = 9 }]
        };

        var result = TemplateDiffCalculator.Diff(before, after);

        Assert.False(result.IsEmpty);
        Assert.True(Assert.Single(result.Categories.Changed).ReorderOnly);
        Assert.True(Assert.Single(result.Components.Changed).ReorderOnly);
        Assert.True(Assert.Single(result.ChecklistItems.Changed).ReorderOnly);
        Assert.Empty(result.Components.Changed[0].Fields);
        Assert.Empty(result.ChecklistItems.Changed[0].Fields);
    }

    [Fact]
    public void IgnoresWhitespaceAndEquivalentSpecificationCaseAndOrder()
    {
        var before = Fixtures.Snapshot();
        var after = before with
        {
            Name = " School project ", Description = " \t ",
            Categories = [before.Categories[0] with { Name = " Electronics " }],
            Components = [before.Components[0] with
            {
                Title = " Temperature sensor ", Description = " ",
                Part = new(" Sensor ", " S-100 ", " P-100 ", ["accuracy", " VOLTAGE ", " "])
            }],
            ChecklistItems = [before.ChecklistItems[0] with
            {
                Title = " Write report ", Description = " ", DefaultAssigneeRole = " "
            }]
        };

        Assert.True(TemplateDiffCalculator.Diff(before, after).IsEmpty);
        Assert.Equal(" School project ", after.Name);
        Assert.Equal(" Temperature sensor ", after.Components[0].Title);
    }

    [Fact]
    public void ComparesNamesUsingOrdinalCaseSensitivity()
    {
        var before = Fixtures.Snapshot();
        var after = before with { Name = "school project" };

        Assert.Equal(new FieldChangeDto("Name", "School project", "school project"),
            Assert.Single(TemplateDiffCalculator.Diff(before, after).FieldChanges));
    }

    [Fact]
    public void NullCollectionsAreEquivalentToEmptyCollections()
    {
        var before = new TemplateSnapshot(1, "Empty", null, null!, null!, null!, null);
        var after = new TemplateSnapshot(1, "Empty", null, [], [], [], []);

        Assert.True(TemplateDiffCalculator.Diff(before, after).IsEmpty);
        var withPart = Fixtures.Snapshot();
        var noSpecs = withPart with { Components = [withPart.Components[0] with { Part = withPart.Components[0].Part! with { SpecNames = null! } }] };
        var emptySpecs = noSpecs with { Components = [noSpecs.Components[0] with { Part = noSpecs.Components[0].Part! with { SpecNames = [] } }] };
        Assert.True(TemplateDiffCalculator.Diff(noSpecs, emptySpecs).IsEmpty);
    }

    [Fact]
    public void ChangedFlagsMakeTheResultNonempty()
    {
        var before = Fixtures.Snapshot() with { PresetFlags = new() { ["Reviewed"] = false } };
        var after = before with { PresetFlags = new() { ["Reviewed"] = true } };

        var result = TemplateDiffCalculator.Diff(before, after);

        Assert.True(result.PresetFlagsChanged);
        Assert.False(result.IsEmpty);
        Assert.Empty(result.FieldChanges);
        Assert.False(TemplateDiffCalculator.Diff(after, after).PresetFlagsChanged);
    }

    [Fact]
    public void IgnoresFlagInsertionOrderAndDetectsFlagKeyCaseChanges()
    {
        var before = Fixtures.Snapshot() with { PresetFlags = new() { ["Reviewed"] = false, ["Approved"] = true } };
        var reordered = before with { PresetFlags = new() { ["Approved"] = true, ["Reviewed"] = false } };
        var renamed = before with { PresetFlags = new() { ["Reviewed"] = false, ["approved"] = true } };

        Assert.True(TemplateDiffCalculator.Diff(before, reordered).IsEmpty);
        Assert.True(TemplateDiffCalculator.Diff(before, renamed).PresetFlagsChanged);
    }
}

internal static class Fixtures
{
    internal static readonly Guid CategoryKey = Guid.Parse("10000000-0000-0000-0000-000000000001");
    internal static readonly Guid ComponentKey = Guid.Parse("20000000-0000-0000-0000-000000000001");
    internal static readonly Guid PartId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    internal static readonly Guid ChecklistKey = Guid.Parse("40000000-0000-0000-0000-000000000001");
    internal static readonly Guid AddedKey = Guid.Parse("50000000-0000-0000-0000-000000000001");
    internal static readonly Guid RemovedKey = Guid.Parse("60000000-0000-0000-0000-000000000001");

    internal static TemplateSnapshot Snapshot() => new(1, "School project", null,
        [new(CategoryKey, "Electronics", 0)],
        [new(ComponentKey, CategoryKey, "Temperature sensor", null, 0, true, 1, PartId,
            new("Sensor", "S-100", "P-100", ["Voltage", "Accuracy"]))],
        [new(ChecklistKey, "Write report", null, 0, true, null, null)]);
}
