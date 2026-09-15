using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace SpecTrack.Api.Tests;

public sealed class ComparisonTests
{
    [Fact]
    public async Task Preview_reports_a_name_edit_and_does_not_persist_snapshots()
    {
        using var storage = new TestStorage();
        using var app = new ApiTestHost(storage.Path);
        using var client = app.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/compare/preview", new
        {
            from = ApiTestHost.Snapshot("Initial project"),
            to = ApiTestHost.Snapshot("Reviewed project")
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ApiTestHost.ReadObject(response);
        Assert.False(result["diff"]!["isEmpty"]!.GetValue<bool>());
        Assert.Equal(1, result["summary"]!["fieldChanges"]!.GetValue<int>());
        Assert.Equal(0, result["summary"]!["added"]!.GetValue<int>());
        Assert.Equal(0, result["summary"]!["removed"]!.GetValue<int>());
        var edits = Assert.IsType<JsonArray>(result["diff"]!["fieldChanges"]);
        Assert.Single(edits);
        Assert.Contains("Initial project", edits.ToJsonString());
        Assert.Contains("Reviewed project", edits.ToJsonString());
        Assert.Empty((await client.GetFromJsonAsync<JsonArray>("/api/snapshots"))!);
    }

    [Fact]
    public async Task Saved_comparison_counts_added_removed_changed_and_reordered_rows()
    {
        using var storage = new TestStorage();
        using var app = new ApiTestHost(storage.Path);
        using var client = app.CreateClient();
        var before = ApiTestHost.Snapshot();
        before["categories"] = new JsonArray(ApiTestHost.Category());
        before["components"] = new JsonArray(ApiTestHost.Component(), ApiTestHost.Component(
            "44444444-4444-4444-4444-444444444444", "Old cable"));
        before["checklistItems"] = new JsonArray(ApiTestHost.Checklist());
        var after = ApiTestHost.Snapshot();
        after["categories"] = new JsonArray(ApiTestHost.Category(sortOrder: 2));
        after["components"] = new JsonArray(ApiTestHost.Component(quantity: 3));
        after["checklistItems"] = new JsonArray(ApiTestHost.Checklist(), ApiTestHost.Checklist(
            "55555555-5555-5555-5555-555555555555", "Record results"));
        var from = await ApiTestHost.Create(client, before, "Before review");
        var to = await ApiTestHost.Create(client, after, "After review");

        using var response = await client.PostAsJsonAsync("/api/compare", new
        {
            fromId = from["id"]!.GetValue<string>(), toId = to["id"]!.GetValue<string>()
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ApiTestHost.ReadObject(response);
        Assert.Equal("Before review", result["from"]!["label"]!.GetValue<string>());
        Assert.Equal("After review", result["to"]!["label"]!.GetValue<string>());
        Assert.Equal(from["id"]!.GetValue<string>(), result["from"]!["id"]!.GetValue<string>());
        Assert.Equal(to["id"]!.GetValue<string>(), result["to"]!["id"]!.GetValue<string>());
        var summary = result["summary"]!;
        Assert.Equal(1, summary["added"]!.GetValue<int>());
        Assert.Equal(1, summary["removed"]!.GetValue<int>());
        Assert.Equal(2, summary["changed"]!.GetValue<int>());
        Assert.Equal(1, summary["reordered"]!.GetValue<int>());
        Assert.Equal(0, summary["fieldChanges"]!.GetValue<int>());
        Assert.False(summary["isEmpty"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Comparing_the_same_snapshot_is_empty()
    {
        using var storage = new TestStorage();
        using var app = new ApiTestHost(storage.Path);
        using var client = app.CreateClient();
        var created = await ApiTestHost.Create(client, ApiTestHost.Snapshot());
        var id = created["id"]!.GetValue<string>();

        using var response = await client.PostAsJsonAsync("/api/compare", new { fromId = id, toId = id });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ApiTestHost.ReadObject(response);
        Assert.True(result["diff"]!["isEmpty"]!.GetValue<bool>());
        Assert.True(result["summary"]!["isEmpty"]!.GetValue<bool>());
        Assert.Equal(0, result["summary"]!["changed"]!.GetValue<int>());
    }

    [Fact]
    public async Task Downloaded_html_report_encodes_user_content()
    {
        using var storage = new TestStorage();
        using var app = new ApiTestHost(storage.Path);
        using var client = app.CreateClient();
        const string maliciousName = "<script>alert('name')</script> & school";
        const string maliciousTitle = "<img src=x onerror=alert('title')>";
        var before = ApiTestHost.Snapshot("Initial");
        var after = ApiTestHost.Snapshot(maliciousName);
        after["components"] = new JsonArray(ApiTestHost.Component(title: maliciousTitle));
        var from = await ApiTestHost.Create(client, before, "First");
        var to = await ApiTestHost.Create(client, after, "<svg onload=alert('label')>");

        using var response = await client.GetAsync($"/api/compare/{from["id"]}/{to["id"]}/report");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(maliciousName, html);
        Assert.DoesNotContain(maliciousTitle, html);
        Assert.DoesNotContain("<svg onload=", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("&lt;img", html);
        Assert.Contains("&amp; school", html);
    }
}
