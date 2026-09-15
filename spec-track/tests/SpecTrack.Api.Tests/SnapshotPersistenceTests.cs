using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace SpecTrack.Api.Tests;

public sealed class SnapshotPersistenceTests
{
    [Theory]
    [InlineData("categories")]
    [InlineData("components")]
    [InlineData("checklistItems")]
    public async Task Legacy_null_collection_is_saved_and_returned_as_an_empty_array(string collection)
    {
        using var storage = new TestStorage();
        JsonObject created;
        using (var app = new ApiTestHost(storage.Path))
        using (var client = app.CreateClient())
        {
            var snapshot = ApiTestHost.Snapshot();
            snapshot[collection] = null;
            created = await ApiTestHost.Create(client, snapshot);
            Assert.Empty(Assert.IsType<JsonArray>(created["snapshot"]![collection]));
        }

        using var restarted = new ApiTestHost(storage.Path);
        using var restartedClient = restarted.CreateClient();
        using var fetched = await restartedClient.GetAsync($"/api/snapshots/{created["id"]}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        var persisted = await ApiTestHost.ReadObject(fetched);
        Assert.Empty(Assert.IsType<JsonArray>(persisted["snapshot"]![collection]));
        Assert.True(JsonNode.DeepEquals(created, persisted));
    }

    [Fact]
    public async Task Host_starts_with_only_local_storage_configuration()
    {
        using var storage = new TestStorage();
        using var app = new ApiTestHost(storage.Path);
        using var client = app.CreateClient();

        using var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        var status = await ApiTestHost.ReadObject(health);
        Assert.Equal("ok", status["status"]!.GetValue<string>());
        Assert.Equal("json-files", status["storage"]!.GetValue<string>());
        Assert.False(status["database"]!.GetValue<bool>());

        var created = await ApiTestHost.Create(client, ApiTestHost.Snapshot());
        using var fetched = await client.GetAsync($"/api/snapshots/{created["id"]}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.True(Directory.EnumerateFiles(storage.Path, "*.json", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public async Task Snapshot_round_trips_through_record_and_metadata_list()
    {
        using var storage = new TestStorage();
        using var app = new ApiTestHost(storage.Path);
        using var client = app.CreateClient();
        var snapshot = ApiTestHost.Snapshot("Robot controller");
        snapshot["description"] = "Demonstration with diacritics: școală, țară.";
        var earliest = DateTimeOffset.UtcNow;

        var created = await ApiTestHost.Create(client, snapshot, "First review");
        Assert.True(Guid.TryParse(created["id"]!.GetValue<string>(), out var id));
        Assert.InRange(created["createdAt"]!.GetValue<DateTimeOffset>(), earliest, DateTimeOffset.UtcNow);
        Assert.Equal("First review", created["label"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(snapshot, created["snapshot"]));

        using var get = await client.GetAsync($"/api/snapshots/{id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.True(JsonNode.DeepEquals(created, await ApiTestHost.ReadObject(get)));

        var listed = await client.GetFromJsonAsync<JsonArray>("/api/snapshots");
        var metadata = Assert.Single(listed!);
        Assert.Equal(id.ToString(), metadata!["id"]!.GetValue<string>());
        Assert.Equal("First review", metadata["label"]!.GetValue<string>());
        Assert.Equal("Robot controller", metadata["name"]!.GetValue<string>());
        Assert.Equal(created["createdAt"]!.GetValue<DateTimeOffset>(), metadata["createdAt"]!.GetValue<DateTimeOffset>());
        Assert.Null(metadata["snapshot"]);
    }

    [Fact]
    public async Task Snapshots_survive_a_new_host_and_storage_directory_relocation()
    {
        using var originalStorage = new TestStorage();
        using var copiedStorage = new TestStorage();
        JsonObject first;
        JsonObject second;
        using (var app = new ApiTestHost(originalStorage.Path))
        using (var client = app.CreateClient())
        {
            first = await ApiTestHost.Create(client, ApiTestHost.Snapshot("Original"), "v1");
            second = await ApiTestHost.Create(client, ApiTestHost.Snapshot("Revised"), "v2");
        }

        using (var restarted = new ApiTestHost(originalStorage.Path))
        using (var client = restarted.CreateClient())
        {
            using var response = await client.GetAsync($"/api/snapshots/{first["id"]}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(JsonNode.DeepEquals(first, await ApiTestHost.ReadObject(response)));
        }
        originalStorage.CopyTo(copiedStorage);

        using var relocated = new ApiTestHost(copiedStorage.Path);
        using var relocatedClient = relocated.CreateClient();
        var listed = await relocatedClient.GetFromJsonAsync<JsonArray>("/api/snapshots");
        Assert.Equal(2, listed!.Count);
        using var compare = await relocatedClient.PostAsJsonAsync("/api/compare", new
        {
            fromId = first["id"]!.GetValue<string>(),
            toId = second["id"]!.GetValue<string>()
        });
        Assert.Equal(HttpStatusCode.OK, compare.StatusCode);
        var result = await ApiTestHost.ReadObject(compare);
        Assert.Equal("Original", result["from"]!["name"]!.GetValue<string>());
        Assert.Equal("Revised", result["to"]!["name"]!.GetValue<string>());
        Assert.False(result["diff"]!["isEmpty"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Concurrent_creates_keep_every_record_and_assign_unique_ids()
    {
        using var storage = new TestStorage();
        using var app = new ApiTestHost(storage.Path);
        using var client = app.CreateClient();

        var created = await Task.WhenAll(Enumerable.Range(0, 24).Select(index =>
            ApiTestHost.Create(client, ApiTestHost.Snapshot($"Project {index}"), $"v{index}")));
        var ids = created.Select(record => record["id"]!.GetValue<string>()).ToArray();
        Assert.Equal(24, ids.Distinct().Count());
        var listed = await client.GetFromJsonAsync<JsonArray>("/api/snapshots");
        Assert.Equal(ids.Order(), listed!.Select(record => record!["id"]!.GetValue<string>()).Order());
        foreach (var record in created)
        {
            using var response = await client.GetAsync($"/api/snapshots/{record["id"]}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(JsonNode.DeepEquals(record, await ApiTestHost.ReadObject(response)));
        }
    }

    [Fact]
    public async Task Saving_another_version_with_same_label_does_not_overwrite_original()
    {
        using var storage = new TestStorage();
        using var app = new ApiTestHost(storage.Path);
        using var client = app.CreateClient();
        var original = await ApiTestHost.Create(client, ApiTestHost.Snapshot("Original"), "Review");
        var revision = await ApiTestHost.Create(client, ApiTestHost.Snapshot("Revision"), "Review");

        Assert.NotEqual(original["id"]!.GetValue<string>(), revision["id"]!.GetValue<string>());
        using var update = await client.PutAsJsonAsync($"/api/snapshots/{original["id"]}", new
        {
            label = "Changed", snapshot = ApiTestHost.Snapshot("Attempted overwrite")
        });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, update.StatusCode);
        using var get = await client.GetAsync($"/api/snapshots/{original["id"]}");
        Assert.True(JsonNode.DeepEquals(original, await ApiTestHost.ReadObject(get)));
    }
}
