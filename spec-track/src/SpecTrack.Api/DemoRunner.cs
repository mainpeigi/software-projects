using System.Text.Json;
using SpecTrack.Core;

namespace SpecTrack.Api;

public static class DemoRunner
{
    public static async Task RunAsync(FileSnapshotStore store)
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        async Task<TemplateSnapshot> Read(string filename)
        {
            var content = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "examples", filename));
            return JsonSerializer.Deserialize<TemplateSnapshot>(content, json)
                ?? throw new InvalidDataException("Demonstration snapshot is missing.");
        }
        var from = await store.SaveAsync("Demo v1", await Read("school-project-v1.json"));
        var to = await store.SaveAsync("Demo v2", await Read("school-project-v2.json"));
        var diff = TemplateDiffCalculator.Diff(from.Snapshot, to.Snapshot);
        var result = new { fromId = from.Id, toId = to.Id, summary = ComparisonSummary.From(diff), diff };
        var resultPath = Path.Combine(store.DataDirectory, "demo-result.json");
        var reportPath = Path.Combine(store.DataDirectory, "demo-report.html");
        await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(result, json));
        await File.WriteAllTextAsync(reportPath, HtmlChangeReport.Render(from, to, diff));
        Console.WriteLine(JsonSerializer.Serialize(new { result.fromId, result.toId, result.summary, resultPath, reportPath }, json));
    }
}
