using System.Net;
using System.Text;
using System.Text.Json;
using Datemulte_2.Data;
using Datemulte_2.Services;
using Datemulte_2.Services.DataManagement;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OfficeOpenXml;
using Xunit;

namespace Datemulte.Portable.Tests;

public class PortableIntegrationTests
{
    private const string CsvContent = "Sample,Temperature,Humidity\n1,10,80\n2,20,60\n3,30,40\n";

    [Fact]
    public async Task Health_starts_without_external_database_configuration()
    {
        using var directory = new TemporaryDataDirectory();
        await using var factory = new PortableApplicationFactory(directory.Path);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var health = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", health.RootElement.GetProperty("status").GetString());
        Assert.Equal("sqlite", health.RootElement.GetProperty("storage").GetString());
        Assert.False(health.RootElement.GetProperty("externalDatabase").GetBoolean());
        using var scope = factory.Services.CreateScope();
        var contexts = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DataManagementDbContext>>();
        await using var database = await contexts.CreateDbContextAsync();
        Assert.IsType<SqliteConnection>(database.Database.GetDbConnection());
        var databasePath = System.IO.Path.GetFullPath(database.Database.GetDbConnection().DataSource);
        Assert.StartsWith(directory.Path + System.IO.Path.DirectorySeparatorChar, databasePath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(databasePath));
    }

    [Theory]
    [InlineData("/", "text/html")]
    [InlineData("/csv-plotter", "text/html")]
    [InlineData("/profile", "text/html")]
    [InlineData("/lib/echarts/echarts.min.js", "javascript")]
    [InlineData("/_framework/blazor.web.js", "javascript")]
    [InlineData("/_content/MudBlazor/MudBlazor.min.js", "javascript")]
    public async Task Pages_and_local_browser_assets_are_served(string path, string mediaType)
    {
        using var directory = new TemporaryDataDirectory();
        await using var factory = new PortableApplicationFactory(directory.Path);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(mediaType, response.Content.Headers.ContentType?.MediaType ?? "");
        Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Csv_upload_saves_numeric_data_and_calculates_expected_statistics()
    {
        using var directory = new TemporaryDataDirectory();
        await using var factory = new PortableApplicationFactory(directory.Path);
        using var scope = factory.Services.CreateScope();
        var userId = await GetLocalUserIdAsync(scope.ServiceProvider);
        var sessions = scope.ServiceProvider.GetRequiredService<DataSessionService>();

        var (sessionId, data) = await sessions.ProcessFileUploadAsync(CsvFile(), userId);

        Assert.NotEqual(Guid.Empty, sessionId);
        Assert.Equal(3, data.Count);
        Assert.Equal("10", data[0].GetValue("Temperature"));
        Assert.Equal("40", data[2].GetValue("Humidity"));
        var statistics = scope.ServiceProvider.GetRequiredService<StatisticsService>();
        var result = statistics.CalculateStatistics(data, "Temperature", movingAverageWindow: 2);
        Assert.Equal(3, result.Count);
        Assert.Equal(10d, result.Min);
        Assert.Equal(30d, result.Max);
        Assert.Equal(20d, result.Mean);
        Assert.Equal(20d, result.Median);
        Assert.Equal(60d, result.Sum);
        Assert.Equal(66.66666666666667d, result.Variance, precision: 10);
        Assert.Equal(25d, result.MovingAverage);
        var correlations = statistics.CalculateCorrelationMatrix(data, ["Temperature", "Humidity"]);
        Assert.Equal(-1d, correlations["Temperature"]["Humidity"], precision: 10);

        var (stored, restored) = await sessions.LoadSessionAsync(sessionId, userId);
        Assert.Equal("measurements.csv", stored.SourceFileName);
        Assert.Contains("Temperature", stored.NumericColumns);
        Assert.Equal(3, restored.Count);
        Assert.Equal(sessionId, Assert.Single(await sessions.GetUserSessionsAsync(userId)).SessionId);
    }

    [Fact]
    public async Task Excel_upload_uses_the_selected_worksheet_and_persists_it()
    {
        using var directory = new TemporaryDataDirectory();
        await using var factory = new PortableApplicationFactory(directory.Path);
        using var scope = factory.Services.CreateScope();
        var userId = await GetLocalUserIdAsync(scope.ServiceProvider);
        using var workbook = new ExcelPackage();
        var otherSheet = workbook.Workbook.Worksheets.Add("Other");
        otherSheet.Cells[1, 1].Value = "Wrong worksheet";
        otherSheet.Cells[2, 1].Value = 999;
        var sheet = workbook.Workbook.Worksheets.Add("Measurements");
        sheet.Cells[1, 1].Value = "Sample";
        sheet.Cells[1, 2].Value = "Voltage";
        sheet.Cells[2, 1].Value = 1;
        sheet.Cells[2, 2].Value = 12;
        sheet.Cells[3, 1].Value = 2;
        sheet.Cells[3, 2].Value = 18;
        sheet.Cells[4, 1].Value = 3;
        sheet.Cells[4, 2].Value = 24;
        var file = new MemoryBrowserFile("measurements.xlsx", workbook.GetAsByteArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var sessions = scope.ServiceProvider.GetRequiredService<DataSessionService>();

        var (sessionId, data) = await sessions.ProcessFileUploadAsync(file, userId, "Measurements");

        Assert.Equal(3, data.Count);
        Assert.Equal(12d, data[0].GetNumericValue("Voltage"));
        Assert.Equal(24d, data[2].GetNumericValue("Voltage"));
        var result = scope.ServiceProvider.GetRequiredService<StatisticsService>().CalculateStatistics(data, "Voltage");
        Assert.Equal(18d, result.Mean);
        var (session, restored) = await sessions.LoadSessionAsync(sessionId, userId);
        Assert.Equal(new[] { "Sample", "Voltage" }, session.Columns);
        Assert.Equal(18d, restored[1].GetNumericValue("Voltage"));
    }

    [Fact]
    public async Task Local_identity_and_profile_work_without_supabase_and_survive_restart()
    {
        using var directory = new TemporaryDataDirectory();
        Guid originalUserId;
        await using (var firstFactory = new PortableApplicationFactory(directory.Path))
        {
            using var scope = firstFactory.Services.CreateScope();
            originalUserId = await GetLocalUserIdAsync(scope.ServiceProvider);
            var auth = scope.ServiceProvider.GetRequiredService<LocalProfileService>();
            var profiles = scope.ServiceProvider.GetRequiredService<UserProfileService>();
            var profile = await profiles.GetOrCreateProfileAsync(originalUserId, auth.GetUserEmail());
            Assert.Equal(originalUserId, profile.UserId);
            await profiles.UpdateProfileAsync(originalUserId, "Erasmus demonstration");
        }

        await using var secondFactory = new PortableApplicationFactory(directory.Path);
        using var restoredScope = secondFactory.Services.CreateScope();
        Assert.Equal(originalUserId, await GetLocalUserIdAsync(restoredScope.ServiceProvider));
        var restored = await restoredScope.ServiceProvider.GetRequiredService<UserProfileService>().GetProfileAsync(originalUserId);
        Assert.NotNull(restored);
        Assert.Equal("Erasmus demonstration", restored.DisplayName);
    }

    [Fact]
    public async Task Session_and_edits_survive_restart_and_copying_data_to_another_folder()
    {
        using var original = new TemporaryDataDirectory();
        using var relocated = new TemporaryDataDirectory();
        Guid sessionId;
        Guid userId;
        await using (var firstFactory = new PortableApplicationFactory(original.Path))
        {
            using var scope = firstFactory.Services.CreateScope();
            userId = await GetLocalUserIdAsync(scope.ServiceProvider);
            var sessions = scope.ServiceProvider.GetRequiredService<DataSessionService>();
            (sessionId, _) = await sessions.ProcessFileUploadAsync(CsvFile(), userId);
            await sessions.SaveCellEditAsync(sessionId, 1, "Temperature", "20", "50", userId);
            await sessions.UpdateSessionNameAsync(sessionId, "Transferred experiment", userId);
        }

        await using (var restartedFactory = new PortableApplicationFactory(original.Path))
        {
            using var scope = restartedFactory.Services.CreateScope();
            Assert.Equal(userId, await GetLocalUserIdAsync(scope.ServiceProvider));
            var sessions = scope.ServiceProvider.GetRequiredService<DataSessionService>();
            var (session, data) = await sessions.LoadSessionAsync(sessionId, userId);
            Assert.Equal("Transferred experiment", session.SessionName);
            Assert.Equal("50", data[1].GetValue("Temperature"));
            Assert.Equal("10", data[0].GetValue("Temperature"));
            Assert.Equal(1, (await sessions.GetSessionStatisticsAsync(sessionId)).TotalEdits);
        }

        SqliteConnection.ClearAllPools();
        var sourceFiles = Directory.GetFiles(System.IO.Path.Combine(original.Path, "files"), "*", SearchOption.AllDirectories);
        Assert.Single(sourceFiles);
        foreach (var sourcePath in Directory.EnumerateFiles(original.Path, "*", SearchOption.AllDirectories))
        {
            var targetPath = System.IO.Path.Combine(relocated.Path, System.IO.Path.GetRelativePath(original.Path, sourcePath));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(targetPath)!);
            File.Copy(sourcePath, targetPath);
        }

        await using var relocatedFactory = new PortableApplicationFactory(relocated.Path);
        using var relocatedScope = relocatedFactory.Services.CreateScope();
        Assert.Equal(userId, await GetLocalUserIdAsync(relocatedScope.ServiceProvider));
        var relocatedSessions = relocatedScope.ServiceProvider.GetRequiredService<DataSessionService>();
        var (movedSession, movedData) = await relocatedSessions.LoadSessionAsync(sessionId, userId);
        Assert.Equal("Transferred experiment", movedSession.SessionName);
        Assert.Equal(3, movedData.Count);
        Assert.Equal("50", movedData[1].GetValue("Temperature"));
        Assert.Equal(sessionId, Assert.Single(await relocatedSessions.GetUserSessionsAsync(userId)).SessionId);

        await relocatedSessions.DeleteSessionAsync(sessionId, userId);

        Assert.Empty(await relocatedSessions.GetUserSessionsAsync(userId));
        Assert.Empty(Directory.GetFiles(System.IO.Path.Combine(relocated.Path, "files"), "*", SearchOption.AllDirectories));
        Assert.All(sourceFiles, path => Assert.True(File.Exists(path), "Deleting from the copied folder must preserve the original source file."));
        var contextFactory = relocatedScope.ServiceProvider.GetRequiredService<IDbContextFactory<DataManagementDbContext>>();
        await using var database = await contextFactory.CreateDbContextAsync();
        Assert.Empty(await database.SourceFiles.ToListAsync());
        Assert.Empty(await database.DataSnapshots.ToListAsync());
        Assert.Empty(await database.DataChangeEvents.ToListAsync());
    }

    private static MemoryBrowserFile CsvFile() => new("measurements.csv", Encoding.UTF8.GetBytes(CsvContent), "text/csv");

    private static async Task<Guid> GetLocalUserIdAsync(IServiceProvider services)
    {
        var auth = services.GetRequiredService<LocalProfileService>();
        await auth.InitializedAsync();
        Assert.False(auth.IsAuthenticated);
        Assert.True(auth.IsLocalMode);
        Assert.True(auth.CurrentUserId.HasValue);
        Assert.NotEqual(Guid.Empty, auth.CurrentUserId.Value);
        return auth.CurrentUserId.Value;
    }
}

internal sealed class MemoryBrowserFile(string name, byte[] bytes, string contentType) : IBrowserFile
{
    public string Name => name;
    public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
    public long Size => bytes.LongLength;
    public string ContentType => contentType;

    public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Size > maxAllowedSize)
            throw new IOException("File exceeds the upload size limit.");
        return new MemoryStream(bytes, writable: false);
    }
}

internal sealed class PortableApplicationFactory(string dataDirectory) : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Minimal hosting reads these values before the deferred web-host callback runs.
        builder.ConfigureHostConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Portable:DataDirectory"] = dataDirectory,
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft"] = "Warning"
            });
        });
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = null,
                ["Supabase:Url"] = null,
                ["Supabase:Key"] = null
            }));
    }
}

internal sealed class TemporaryDataDirectory : IDisposable
{
    private readonly string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Datemulte.Portable.Tests"));
    public string Path { get; }

    public TemporaryDataDirectory()
    {
        Path = System.IO.Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        var resolved = System.IO.Path.GetFullPath(Path);
        if (!resolved.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Test cleanup target is outside the temporary test root.");
        if (Directory.Exists(resolved))
            Directory.Delete(resolved, recursive: true);
    }
}
