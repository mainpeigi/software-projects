using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;

namespace SpecTrack.Api.Tests;

public sealed class ValidationTests
{
    [Theory]
    [InlineData("unsupported-schema")]
    [InlineData("empty-name")]
    [InlineData("null-name")]
    [InlineData("missing-name")]
    [InlineData("null-category-row")]
    [InlineData("null-component-row")]
    [InlineData("null-checklist-row")]
    [InlineData("duplicate-category-key")]
    [InlineData("duplicate-component-key")]
    [InlineData("duplicate-checklist-key")]
    [InlineData("empty-key")]
    [InlineData("dangling-category")]
    [InlineData("negative-quantity")]
    [InlineData("empty-category-name")]
    [InlineData("empty-component-title")]
    [InlineData("empty-checklist-title")]
    public async Task Invalid_snapshot_is_rejected_without_writing_a_file(string invalidCase)
    {
        using var storage = new TestStorage();
        using var app = new ApiTestHost(storage.Path);
        using var client = app.CreateClient();
        var snapshot = InvalidSnapshot(invalidCase);

        using var response = await client.PostAsJsonAsync("/api/snapshots", new { label = "Invalid", snapshot });
        await AssertProblem(response, HttpStatusCode.BadRequest, storage.Path);
        Assert.Empty((await client.GetFromJsonAsync<JsonArray>("/api/snapshots"))!);
        Assert.Empty(Directory.EnumerateFiles(storage.Path, "*.json", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("/api/snapshots", "null")]
    [InlineData("/api/snapshots", "{}")]
    [InlineData("/api/snapshots", "{\"label\":\"v1\",\"snapshot\":null}")]
    [InlineData("/api/snapshots", "{invalid-json")]
    [InlineData("/api/compare", "null")]
    [InlineData("/api/compare", "{}")]
    [InlineData("/api/compare", "{\"fromId\":\"not-a-guid\",\"toId\":\"not-a-guid\"}")]
    [InlineData("/api/compare/preview", "null")]
    [InlineData("/api/compare/preview", "{}")]
    [InlineData("/api/compare/preview", "{\"from\":null,\"to\":null}")]
    public async Task Invalid_request_returns_a_sanitized_problem(string path, string json)
    {
        using var storage = new TestStorage();
        using var app = new ApiTestHost(storage.Path);
        using var client = app.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(path, content);
        await AssertProblem(response, HttpStatusCode.BadRequest, storage.Path);
    }

    [Fact]
    public async Task Invalid_preview_snapshot_is_validated_without_creating_records()
    {
        using var storage = new TestStorage();
        using var app = new ApiTestHost(storage.Path);
        using var client = app.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/compare/preview", new
        {
            from = ApiTestHost.Snapshot(), to = InvalidSnapshot("dangling-category")
        });
        await AssertProblem(response, HttpStatusCode.BadRequest, storage.Path);
        Assert.Empty((await client.GetFromJsonAsync<JsonArray>("/api/snapshots"))!);
    }

    [Theory]
    [InlineData("/api/snapshots/99999999-9999-9999-9999-999999999999")]
    [InlineData("/api/compare/99999999-9999-9999-9999-999999999999/88888888-8888-8888-8888-888888888888/report")]
    public async Task Missing_record_returns_not_found(string path)
    {
        using var storage = new TestStorage();
        using var app = new ApiTestHost(storage.Path);
        using var client = app.CreateClient();
        using var response = await client.GetAsync(path);
        await AssertProblem(response, HttpStatusCode.NotFound, storage.Path);
    }

    [Theory]
    [InlineData("/api/snapshots/not-a-guid")]
    [InlineData("/api/snapshots/%2e%2e%2fsecret")]
    [InlineData("/api/compare/not-a-guid/also-invalid/report")]
    public async Task Malformed_route_ids_do_not_reach_storage_or_reveal_internals(string path)
    {
        using var storage = new TestStorage();
        using var app = new ApiTestHost(storage.Path);
        using var client = app.CreateClient();
        using var response = await client.GetAsync(path);
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.NotFound });
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(storage.Path, body);
        Assert.DoesNotContain("System.", body);
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Comparing_a_missing_record_returns_not_found()
    {
        using var storage = new TestStorage();
        using var app = new ApiTestHost(storage.Path);
        using var client = app.CreateClient();
        var present = await ApiTestHost.Create(client, ApiTestHost.Snapshot());
        using var response = await client.PostAsJsonAsync("/api/compare", new
        {
            fromId = present["id"]!.GetValue<string>(), toId = Guid.NewGuid()
        });
        await AssertProblem(response, HttpStatusCode.NotFound, storage.Path);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Oversize_request_is_rejected_before_a_snapshot_is_saved(bool unknownLength)
    {
        using var storage = new TestStorage();
        using var app = new ApiTestHost(storage.Path);
        using var client = app.CreateClient();
        var snapshot = ApiTestHost.Snapshot();
        snapshot["description"] = new string('x', 4 * 1024 * 1024);
        var bytes = Encoding.UTF8.GetBytes(new JsonObject
        {
            ["label"] = "Oversize", ["snapshot"] = snapshot
        }.ToJsonString());
        using HttpContent content = unknownLength ? new UnknownLengthContent(bytes) : new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        using var response = await client.PostAsync("/api/snapshots", content);
        await AssertProblem(response, HttpStatusCode.RequestEntityTooLarge, storage.Path);
        Assert.Empty((await client.GetFromJsonAsync<JsonArray>("/api/snapshots"))!);
    }

    private static JsonObject InvalidSnapshot(string invalidCase)
    {
        var snapshot = ApiTestHost.Snapshot();
        switch (invalidCase)
        {
            case "unsupported-schema": snapshot["schemaVersion"] = 2; break;
            case "empty-name": snapshot["name"] = "  "; break;
            case "null-name": snapshot["name"] = null; break;
            case "missing-name": snapshot.Remove("name"); break;
            case "null-category-row": snapshot["categories"] = new JsonArray((JsonNode?)null); break;
            case "null-component-row": snapshot["components"] = new JsonArray((JsonNode?)null); break;
            case "null-checklist-row": snapshot["checklistItems"] = new JsonArray((JsonNode?)null); break;
            case "duplicate-category-key": snapshot["categories"] = new JsonArray(ApiTestHost.Category(), ApiTestHost.Category()); break;
            case "duplicate-component-key": snapshot["components"] = new JsonArray(ApiTestHost.Component(), ApiTestHost.Component()); break;
            case "duplicate-checklist-key": snapshot["checklistItems"] = new JsonArray(ApiTestHost.Checklist(), ApiTestHost.Checklist()); break;
            case "empty-key": snapshot["categories"] = new JsonArray(ApiTestHost.Category(Guid.Empty.ToString())); break;
            case "dangling-category":
                var component = ApiTestHost.Component();
                component["categoryKey"] = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
                snapshot["components"] = new JsonArray(component);
                break;
            case "negative-quantity": snapshot["components"] = new JsonArray(ApiTestHost.Component(quantity: -1)); break;
            case "empty-category-name": snapshot["categories"] = new JsonArray(ApiTestHost.Category(name: " ")); break;
            case "empty-component-title": snapshot["components"] = new JsonArray(ApiTestHost.Component(title: " ")); break;
            case "empty-checklist-title": snapshot["checklistItems"] = new JsonArray(ApiTestHost.Checklist(title: " ")); break;
            default: throw new ArgumentOutOfRangeException(nameof(invalidCase));
        }
        return snapshot;
    }

    internal static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string storagePath)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Expected {(int)status}; received {(int)response.StatusCode}: {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = Assert.IsType<JsonObject>(JsonNode.Parse(body));
        Assert.Equal((int)status, problem["status"]!.GetValue<int>());
        Assert.False(string.IsNullOrWhiteSpace(problem["title"]?.GetValue<string>()));
        Assert.DoesNotContain(storagePath, body);
        Assert.DoesNotContain("System.", body);
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(bytes).AsTask();
    }
}
