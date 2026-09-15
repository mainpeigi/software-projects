using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using SpecTrack.Core;

namespace SpecTrack.Api;

public static class HtmlChangeReport
{
    public static string Render(SnapshotRecord from, SnapshotRecord to, TemplateDiffResult diff)
    {
        var summary = ComparisonSummary.From(diff);
        var html = new StringBuilder("""
            <!doctype html><html lang="en"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <title>SpecTrack change report</title><style>
            body{font:16px/1.55 system-ui,sans-serif;color:#172b36;background:#f2f5f6;margin:0}
            main{max-width:1000px;margin:32px auto;padding:32px;background:white;border-radius:12px}
            h1{margin-bottom:0}h2{border-bottom:2px solid #dae4e7;padding-bottom:8px;margin-top:32px}
            .meta{color:#536975}.counts{display:flex;gap:24px;flex-wrap:wrap;padding:18px;background:#e9f3f2}
            table{width:100%;border-collapse:collapse;margin:12px 0 24px;table-layout:fixed}
            th,td{text-align:left;vertical-align:top;padding:10px;border-bottom:1px solid #dbe4e7;overflow-wrap:anywhere;white-space:pre-wrap}
            th{background:#f2f5f6}.added{color:#17643d}.removed{color:#a02931}.changed{color:#805000}
            article{padding:8px 0}small{color:#536975}li{overflow-wrap:anywhere}
            @media print{body{background:white}main{margin:0;padding:0}article{break-inside:avoid}}
            </style></head><body><main><h1>SpecTrack</h1>
            """);
        html.Append("<p class=meta>Requirement change report · ").Append(E(from.Label)).Append(" → ")
            .Append(E(to.Label)).Append("</p><p>").Append(E(from.Snapshot.Name)).Append(" → ")
            .Append(E(to.Snapshot.Name)).Append("</p>");
        html.Append(CultureInfo.InvariantCulture, $"<div class=counts><span>{summary.Added} added</span><span>{summary.Removed} removed</span><span>{summary.Changed} changed</span><span>{summary.Reordered} reorder-only</span></div>");
        if (diff.IsEmpty) html.Append("<p>No changes detected.</p>");
        html.Append("<h2>Project fields</h2>");
        Fields(html, diff.FieldChanges);
        html.Append("<h2>Categories</h2>");
        Items(html, "Added", diff.Categories.Added.Select(x => x.Name), "added");
        Items(html, "Removed", diff.Categories.Removed.Select(x => x.Name), "removed");
        foreach (var item in diff.Categories.Changed)
        {
            html.Append("<article><strong>").Append(E(item.Name)).Append("</strong>");
            if (item.RenamedFrom is not null) html.Append("<p>Renamed from ").Append(E(item.RenamedFrom)).Append("</p>");
            if (item.SortOrderBefore is not null)
                html.Append(CultureInfo.InvariantCulture, $"<p>Order: {item.SortOrderBefore} → {item.SortOrderAfter}</p>");
            html.Append("</article>");
        }
        html.Append("<h2>Components and requirements</h2>");
        Items(html, "Added", diff.Components.Added.Select(x => x.Title), "added");
        Items(html, "Removed", diff.Components.Removed.Select(x => x.Title), "removed");
        foreach (var item in diff.Components.Changed)
        {
            html.Append("<article><h3>").Append(E(item.Title)).Append("</h3>");
            if (item.ReorderOnly) html.Append("<p>Order changed; content unchanged.</p>");
            Fields(html, item.Fields);
            Items(html, "Specifications added", item.SpecsAdded, "added");
            Items(html, "Specifications removed", item.SpecsRemoved, "removed");
            html.Append("</article>");
        }
        html.Append("<h2>Checklist</h2>");
        Items(html, "Added", diff.ChecklistItems.Added.Select(x => x.Title), "added");
        Items(html, "Removed", diff.ChecklistItems.Removed.Select(x => x.Title), "removed");
        foreach (var item in diff.ChecklistItems.Changed)
        {
            html.Append("<article><h3>").Append(E(item.Title)).Append("</h3>");
            if (item.ReorderOnly) html.Append("<p>Order changed; content unchanged.</p>");
            Fields(html, item.Fields);
            html.Append("</article>");
        }
        if (diff.PresetFlagsChanged)
        {
            html.Append("<h2>Project options</h2>");
            var before = from.Snapshot.PresetFlags ?? new();
            var after = to.Snapshot.PresetFlags ?? new();
            Fields(html, before.Keys.Union(after.Keys).OrderBy(x => x, StringComparer.Ordinal)
                .Where(key => !before.TryGetValue(key, out var a) || !after.TryGetValue(key, out var b) || a != b)
                .Select(key => new FieldChangeDto(key,
                    before.TryGetValue(key, out var a) ? a.ToString() : null,
                    after.TryGetValue(key, out var b) ? b.ToString() : null)));
        }
        html.Append("<hr><small>SpecTrack · local comparison · ").Append(E(from.Id.ToString()))
            .Append(" → ").Append(E(to.Id.ToString())).Append("</small></main></body></html>");
        return html.ToString();
    }

    private static void Fields(StringBuilder html, IEnumerable<FieldChangeDto> changes)
    {
        var rows = changes.ToArray();
        if (rows.Length == 0) return;
        html.Append("<table><thead><tr><th>Field</th><th>Before</th><th>After</th></tr></thead><tbody>");
        foreach (var row in rows)
            html.Append("<tr><td>").Append(E(row.Field)).Append("</td><td>").Append(E(row.Before))
                .Append("</td><td>").Append(E(row.After)).Append("</td></tr>");
        html.Append("</tbody></table>");
    }
    private static void Items(StringBuilder html, string heading, IEnumerable<string> items, string css)
    {
        var rows = items.ToArray();
        if (rows.Length == 0) return;
        html.Append("<p class=\"").Append(css).Append("\">").Append(E(heading)).Append("</p><ul>");
        foreach (var row in rows) html.Append("<li>").Append(E(row)).Append("</li>");
        html.Append("</ul>");
    }
    private static string E(string? value) => HtmlEncoder.Default.Encode(value ?? "—");
}
