using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace SpecTrack.Api.Tests;

internal sealed class ApiTestHost(string storageDirectory, string? allowedOrigin = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Storage:Directory"] = Path.GetFullPath(storageDirectory)
            };
            if (allowedOrigin is not null)
                settings["Cors:Origins:0"] = allowedOrigin;
            configuration.AddInMemoryCollection(settings);
        });
    }

    internal static JsonObject Snapshot(string name = "School project") => new()
    {
        ["schemaVersion"] = 1,
        ["name"] = name,
        ["description"] = null,
        ["categories"] = new JsonArray(),
        ["components"] = new JsonArray(),
        ["checklistItems"] = new JsonArray(),
        ["presetFlags"] = null
    };

    internal static JsonObject Category(string key = "11111111-1111-1111-1111-111111111111",
        string name = "Hardware", int sortOrder = 0) => new()
    {
        ["key"] = key, ["name"] = name, ["sortOrder"] = sortOrder
    };

    internal static JsonObject Component(string key = "22222222-2222-2222-2222-222222222222",
        string title = "Controller", int quantity = 1) => new()
    {
        ["key"] = key, ["categoryKey"] = null, ["title"] = title,
        ["description"] = null, ["sortOrder"] = 0, ["isRequired"] = true,
        ["quantity"] = quantity, ["partId"] = null, ["part"] = null
    };

    internal static JsonObject Checklist(string key = "33333333-3333-3333-3333-333333333333",
        string title = "Verify wiring") => new()
    {
        ["key"] = key, ["title"] = title, ["description"] = null,
        ["sortOrder"] = 0, ["isRequired"] = true, ["dueDateOffsetDays"] = null,
        ["defaultAssigneeRole"] = null, ["severity"] = null
    };

    internal static async Task<JsonObject> Create(HttpClient client, JsonObject snapshot, string label = "v1")
    {
        using var response = await client.PostAsJsonAsync("/api/snapshots", new { label, snapshot });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.Created,
            $"Expected a created snapshot, received {(int)response.StatusCode}: {body}");
        return Assert.IsType<JsonObject>(JsonNode.Parse(body));
    }

    internal static async Task<JsonObject> ReadObject(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return Assert.IsType<JsonObject>(JsonNode.Parse(body));
    }
}

internal sealed class TestStorage : IDisposable
{
    private readonly string expectedRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "SpecTrack.Api.Tests"));

    internal string Path { get; }

    internal TestStorage()
    {
        Path = System.IO.Path.Combine(expectedRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    internal void CopyTo(TestStorage destination)
    {
        foreach (var source in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
        {
            var relative = System.IO.Path.GetRelativePath(Path, source);
            var target = System.IO.Path.Combine(destination.Path, relative);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
            File.Copy(source, target);
        }
    }

    public void Dispose()
    {
        var resolved = System.IO.Path.GetFullPath(Path);
        if (!string.Equals(System.IO.Path.GetDirectoryName(resolved), expectedRoot,
                StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(System.IO.Path.GetFileName(resolved), "N", out _))
            throw new InvalidOperationException("Refusing to remove an unexpected test directory.");
        if (Directory.Exists(resolved))
            Directory.Delete(resolved, recursive: true);
    }
}
