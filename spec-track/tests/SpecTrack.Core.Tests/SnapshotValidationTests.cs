using SpecTrack.Core;
using Xunit;

namespace SpecTrack.Core.Tests;

public class SnapshotValidationTests
{
    [Theory]
    [MemberData(nameof(InvalidSnapshots))]
    public void RejectsInvalidInputsOnEitherSide(string reason, TemplateSnapshot invalid)
    {
        var valid = Fixtures.Snapshot();

        var left = Assert.ThrowsAny<ArgumentException>(() => TemplateDiffCalculator.Diff(invalid, valid));
        var right = Assert.ThrowsAny<ArgumentException>(() => TemplateDiffCalculator.Diff(valid, invalid));

        Assert.False(string.IsNullOrWhiteSpace(left.Message), reason);
        Assert.False(string.IsNullOrWhiteSpace(right.Message), reason);
    }

    public static IEnumerable<object[]> InvalidSnapshots()
    {
        var s = Fixtures.Snapshot();
        yield return ["null snapshot", null!];
        yield return ["unsupported schema", s with { SchemaVersion = 2 }];
        yield return ["missing schema", s with { SchemaVersion = 0 }];
        yield return ["blank name", s with { Name = " \t " }];
        yield return ["null name", s with { Name = null! }];
        yield return ["long name", s with { Name = new('x', 201) }];
        yield return ["long description", s with { Description = new('x', 4001) }];
        yield return ["duplicate category keys", s with { Categories = [s.Categories[0], s.Categories[0]] }];
        yield return ["duplicate component keys", s with { Components = [s.Components[0], s.Components[0]] }];
        yield return ["duplicate checklist keys", s with { ChecklistItems = [s.ChecklistItems[0], s.ChecklistItems[0]] }];
        yield return ["empty category key", s with { Categories = [s.Categories[0] with { Key = Guid.Empty }] }];
        yield return ["empty component key", s with { Components = [s.Components[0] with { Key = Guid.Empty }] }];
        yield return ["empty checklist key", s with { ChecklistItems = [s.ChecklistItems[0] with { Key = Guid.Empty }] }];
        yield return ["missing referenced category", s with { Components = [s.Components[0] with { CategoryKey = Fixtures.RemovedKey }] }];
        yield return ["empty category reference", s with { Components = [s.Components[0] with { CategoryKey = Guid.Empty }] }];
        yield return ["empty part ID", s with { Components = [s.Components[0] with { PartId = Guid.Empty }] }];
        yield return ["zero quantity", s with { Components = [s.Components[0] with { Quantity = 0 }] }];
        yield return ["negative quantity", s with { Components = [s.Components[0] with { Quantity = -1 }] }];
        yield return ["excessive quantity", s with { Components = [s.Components[0] with { Quantity = 1_000_001 }] }];
        yield return ["null category row", s with { Categories = [null!] }];
        yield return ["null component row", s with { Components = [null!] }];
        yield return ["null checklist row", s with { ChecklistItems = [null!] }];
        yield return ["blank category name", s with { Categories = [s.Categories[0] with { Name = " " }] }];
        yield return ["blank component title", s with { Components = [s.Components[0] with { Title = " " }] }];
        yield return ["blank checklist title", s with { ChecklistItems = [s.ChecklistItems[0] with { Title = " " }] }];
        yield return ["blank part name", s with { Components = [s.Components[0] with { Part = s.Components[0].Part! with { Name = " " } }] }];
        yield return ["long component description", s with { Components = [s.Components[0] with { Description = new('x', 4001) }] }];
        yield return ["long checklist description", s with { ChecklistItems = [s.ChecklistItems[0] with { Description = new('x', 4001) }] }];
        yield return ["long assignee role", s with { ChecklistItems = [s.ChecklistItems[0] with { DefaultAssigneeRole = new('x', 201) }] }];
        yield return ["long severity", s with { ChecklistItems = [s.ChecklistItems[0] with { Severity = new('x', 201) }] }];
        yield return ["long MPN", s with { Components = [s.Components[0] with { Part = s.Components[0].Part! with { Mpn = new('x', 201) } }] }];
        yield return ["long SPN", s with { Components = [s.Components[0] with { Part = s.Components[0].Part! with { Spn = new('x', 201) } }] }];
        yield return ["null specification entry", s with { Components = [s.Components[0] with { Part = s.Components[0].Part! with { SpecNames = [null!] } }] }];
        yield return ["long specification", s with { Components = [s.Components[0] with { Part = s.Components[0].Part! with { SpecNames = [new('x', 201)] } }] }];
        yield return ["too many specifications per part", s with { Components = [s.Components[0] with { Part = s.Components[0].Part! with { SpecNames = Enumerable.Range(0, 257).Select(i => $"Spec {i}").ToList() } }] }];
        yield return ["blank flag key", s with { PresetFlags = new() { [" "] = true } }];
        yield return ["long flag key", s with { PresetFlags = new() { [new('x', 201)] = true } }];
        yield return ["too many flags", s with { PresetFlags = Enumerable.Range(0, 257).ToDictionary(i => $"Flag {i}", _ => true) }];
        yield return ["too many total rows", s with { ChecklistItems = Enumerable.Range(0, 4999).Select(i => s.ChecklistItems[0] with { Key = Guid.NewGuid() }).ToList() }];
        yield return ["too many total specifications", s with
        {
            Components = Enumerable.Range(0, 79).Select(i => s.Components[0] with
            {
                Key = Guid.NewGuid(),
                Part = s.Components[0].Part! with { SpecNames = Enumerable.Range(0, 256).Select(n => $"Spec {n}").ToList() }
            }).ToList()
        }];
    }

    [Fact]
    public void KeysMayBeSharedBetweenDifferentSections()
    {
        var s = Fixtures.Snapshot();
        s = s with { Components = [s.Components[0] with { Key = Fixtures.CategoryKey }], ChecklistItems = [s.ChecklistItems[0] with { Key = Fixtures.CategoryKey }] };

        Assert.True(TemplateDiffCalculator.Diff(s, s).IsEmpty);
    }

    [Fact]
    public void AcceptsMaximumQuantityAndRowCount()
    {
        var s = Fixtures.Snapshot();
        s = s with
        {
            Components = [s.Components[0] with { Quantity = 1_000_000 }],
            ChecklistItems = Enumerable.Range(0, 4998).Select(i => s.ChecklistItems[0] with { Key = Guid.NewGuid() }).ToList()
        };

        Assert.True(TemplateDiffCalculator.Diff(s, s).IsEmpty);
    }
}
