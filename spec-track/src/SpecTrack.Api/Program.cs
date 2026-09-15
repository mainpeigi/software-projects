using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http.Features;
using SpecTrack.Api;
using SpecTrack.Core;

const long maximumBodyBytes = 4 * 1024 * 1024;
var demo = args.Contains("--demo", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(arg => arg != "--demo").ToArray());
builder.WebHost.UseUrls("http://127.0.0.1:5192");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maximumBodyBytes);
builder.Services.AddSingleton<FileSnapshotStore>();
builder.Services.AddCors();
var app = builder.Build();
var origins = app.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
foreach (var origin in origins)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var parsed) || !IsLoopbackOrigin(parsed)
        || parsed.AbsolutePath != "/" || parsed.Query.Length > 0 || parsed.Fragment.Length > 0
        || parsed.UserInfo.Length > 0)
        throw new InvalidOperationException("Cors:Origins must contain only HTTP(S) loopback origins, with no path, credentials or query.");
}
origins = origins.Select(origin => new Uri(origin).GetLeftPart(UriPartial.Authority)).Distinct().ToArray();

app.Use(async (context, next) =>
{
    if (context.Connection.RemoteIpAddress is { } remote && !IPAddress.IsLoopback(remote))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; frame-ancestors 'none'";
    try { await next(context); }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
    catch (ArgumentException ex)
    {
        await Results.Problem(statusCode: 400, title: "Invalid input", detail: ex.Message).ExecuteAsync(context);
    }
    catch (BadHttpRequestException ex)
    {
        await Results.Problem(statusCode: ex.StatusCode, title: "Invalid request").ExecuteAsync(context);
    }
    catch (IOException ex)
    {
        app.Logger.LogError(ex, "Local snapshot storage could not complete an operation.");
        await Results.Problem(statusCode: 503, title: "Local storage is unavailable or contains an unreadable snapshot.").ExecuteAsync(context);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Request could not be completed.");
        await Results.Problem(statusCode: 500, title: "The request could not be completed.").ExecuteAsync(context);
    }
});
app.UseCors(policy => policy.WithOrigins(origins).WithMethods("GET", "POST").AllowAnyHeader());
app.UseStatusCodePages(async status =>
    await Results.Problem(statusCode: status.HttpContext.Response.StatusCode,
        title: "The requested operation could not be completed.").ExecuteAsync(status.HttpContext));
app.Use(async (context, next) =>
{
    if (context.Request.ContentLength > maximumBodyBytes)
    {
        await Results.Problem(statusCode: 413, title: "Request exceeds the 4 MiB limit.").ExecuteAsync(context);
        return;
    }
    if (context.Request.Method == "POST" && context.Request.Headers.TryGetValue("Origin", out var header))
    {
        var ownOrigin = $"{context.Request.Scheme}://{context.Request.Host}";
        if (header.Count != 1 || (!string.Equals(header[0], ownOrigin, StringComparison.OrdinalIgnoreCase)
            && !origins.Contains(header[0], StringComparer.OrdinalIgnoreCase)))
        {
            await Results.Problem(statusCode: 403, title: "This browser origin is not allowed.").ExecuteAsync(context);
            return;
        }
    }
    var originalBody = context.Request.Body;
    context.Request.Body = new BoundedBodyStream(originalBody, maximumBodyBytes);
    try { await next(context); }
    finally { context.Request.Body = originalBody; }
});
app.MapGet("/", () => Results.Ok(new
{
    project = "SpecTrack", description = "Local requirement version comparison backend",
    endpoints = new[] { "GET /health", "GET /api/snapshots", "POST /api/snapshots",
        "GET /api/snapshots/{id}", "POST /api/compare", "POST /api/compare/preview",
        "GET /api/compare/{fromId}/{toId}/report" }
}));
app.MapGet("/health", () => Results.Ok(new { status = "ok", storage = "json-files", database = false }));
app.MapGet("/api/snapshots", async (int? limit, FileSnapshotStore store, CancellationToken ct) =>
    Results.Ok(await store.ListAsync(limit ?? 100, ct)));
app.MapPost("/api/snapshots", async (SaveSnapshotRequest request, FileSnapshotStore store, CancellationToken ct) =>
{
    var record = await store.SaveAsync(request.Label, request.Snapshot, ct);
    return Results.Created($"/api/snapshots/{record.Id}", record);
});
app.MapGet("/api/snapshots/{id:guid}", async (Guid id, FileSnapshotStore store, CancellationToken ct) =>
    await store.GetAsync(id, ct) is { } record ? Results.Ok(record) : Results.NotFound());
app.MapPost("/api/compare", async (CompareRequest request, FileSnapshotStore store, CancellationToken ct) =>
{
    if (request.FromId == Guid.Empty || request.ToId == Guid.Empty)
        throw new ArgumentException("Both fromId and toId are required.");
    var from = await store.GetAsync(request.FromId, ct);
    var to = await store.GetAsync(request.ToId, ct);
    if (from is null || to is null) return Results.NotFound();
    var diff = TemplateDiffCalculator.Diff(from.Snapshot, to.Snapshot);
    return Results.Ok(new { from = from.Info, to = to.Info, diff, summary = ComparisonSummary.From(diff) });
});
app.MapPost("/api/compare/preview", (PreviewRequest request) =>
{
    var diff = TemplateDiffCalculator.Diff(request.From, request.To);
    return Results.Ok(new { diff, summary = ComparisonSummary.From(diff) });
});
app.MapGet("/api/compare/{fromId:guid}/{toId:guid}/report", async (Guid fromId, Guid toId,
    FileSnapshotStore store, CancellationToken ct) =>
{
    var from = await store.GetAsync(fromId, ct);
    var to = await store.GetAsync(toId, ct);
    if (from is null || to is null) return Results.NotFound();
    var diff = TemplateDiffCalculator.Diff(from.Snapshot, to.Snapshot);
    return Results.File(Encoding.UTF8.GetBytes(HtmlChangeReport.Render(from, to, diff)),
        "text/html; charset=utf-8", "spectrack-change-report.html");
});

var snapshotStore = app.Services.GetRequiredService<FileSnapshotStore>();
if (demo)
    await DemoRunner.RunAsync(snapshotStore);
else
    app.Run();

static bool IsLoopbackOrigin(Uri origin) => origin.Scheme is "http" or "https"
    && (origin.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || origin.Host == "127.0.0.1" || origin.Host == "[::1]");

public partial class Program { }
