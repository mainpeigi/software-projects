using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace SpecTrack.Api.Tests;

public sealed class OriginTests
{
    [Theory]
    [InlineData("https://foreign.example")]
    [InlineData("file://localhost")]
    [InlineData("http://localhost:5173/path")]
    [InlineData("http://localhost:5173?query=value")]
    [InlineData("http://localhost:5173#fragment")]
    [InlineData("http://user@localhost:5173")]
    public void Nonlocal_or_nonorigin_cors_configuration_prevents_startup(string configuredOrigin)
    {
        using var storage = new TestStorage();
        using var app = new ApiTestHost(storage.Path, configuredOrigin);

        Assert.Throws<InvalidOperationException>(() => app.CreateClient());
    }

    [Theory]
    [InlineData("https://foreign.example")]
    [InlineData("http://localhost:5173")]
    [InlineData("null")]
    public async Task Unconfigured_browser_origin_cannot_create_records(string origin)
    {
        using var storage = new TestStorage();
        using var app = new ApiTestHost(storage.Path);
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/snapshots")
        {
            Content = JsonContent.Create(new { label = "Attempt", snapshot = ApiTestHost.Snapshot() })
        };
        request.Headers.TryAddWithoutValidation("Origin", origin);

        using var response = await client.SendAsync(request);
        await ValidationTests.AssertProblem(response, HttpStatusCode.Forbidden, storage.Path);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Empty((await client.GetFromJsonAsync<JsonArray>("/api/snapshots"))!);
    }

    [Fact]
    public async Task Same_origin_browser_can_create_a_snapshot_without_cors_configuration()
    {
        using var storage = new TestStorage();
        using var app = new ApiTestHost(storage.Path);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", client.BaseAddress!.GetLeftPart(UriPartial.Authority));

        var created = await ApiTestHost.Create(client, ApiTestHost.Snapshot());
        using var response = await client.GetAsync($"/api/snapshots/{created["id"]}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Explicit_loopback_origin_can_preflight_and_create_a_snapshot()
    {
        const string origin = "http://127.0.0.1:5173";
        using var storage = new TestStorage();
        using var app = new ApiTestHost(storage.Path, origin);
        using var client = app.CreateClient();
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/snapshots");
        preflight.Headers.Add("Origin", origin);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type");

        using var preflightResponse = await client.SendAsync(preflight);
        Assert.True(preflightResponse.IsSuccessStatusCode);
        Assert.Equal(origin, Assert.Single(preflightResponse.Headers.GetValues("Access-Control-Allow-Origin")));
        client.DefaultRequestHeaders.Add("Origin", origin);
        using var created = await client.PostAsJsonAsync("/api/snapshots", new
        {
            label = "From local interface", snapshot = ApiTestHost.Snapshot()
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(origin, Assert.Single(created.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task Read_and_preflight_do_not_grant_cors_to_foreign_origin()
    {
        using var storage = new TestStorage();
        using var app = new ApiTestHost(storage.Path, "http://localhost:5173");
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", "https://foreign.example");

        using var get = await client.GetAsync("/api/snapshots");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.False(get.Headers.Contains("Access-Control-Allow-Origin"));
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/snapshots");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        using var preflight = await client.SendAsync(request);
        Assert.False(preflight.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
