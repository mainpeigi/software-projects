using Datemulte_2.Components;
using Datemulte_2.Data;
using Datemulte_2.Middleware;
using Datemulte_2.Services;
using Datemulte_2.Services.Analytics;
using Datemulte_2.Services.DataManagement;
using Datemulte_2.Services.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using OfficeOpenXml;

// This separate edition exposes only the local Blazor application.
// Cloud accounts, shared-session endpoints and background jobs are not hosted.
ExcelPackage.License.SetNonCommercialPersonal("Serban");
var builder = WebApplication.CreateBuilder(args);
var dataDirectory = Path.GetFullPath(
    builder.Configuration["Portable:DataDirectory"] ?? "local-data",
    builder.Environment.ContentRootPath);
Directory.CreateDirectory(dataDirectory);
builder.Configuration["FileStorage:FileSystemBasePath"] = Path.Combine(dataDirectory, "files");
var port = builder.Configuration.GetValue<int?>("Portable:Port") ?? 5188;
if (port < 1024 || port > 65535)
    throw new InvalidOperationException("Portable:Port must be between 1024 and 65535.");
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    options.MultipartBodyLengthLimit = 500 * 1024 * 1024);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 500 * 1024 * 1024);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices(config =>
{
    config.PopoverOptions.ThrowOnDuplicateProvider = false;
    config.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 3000;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 500;
});

var connectionString = new SqliteConnectionStringBuilder
{
    DataSource = Path.Combine(dataDirectory, "datemulte.db")
}.ToString();
builder.Services.AddPooledDbContextFactory<DataManagementDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<LocalProfileService>();
builder.Services.AddScoped<HybridStateManager>();
builder.Services.AddScoped<DataSessionService>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddScoped<CsvService>();
builder.Services.AddScoped<PreferencesService>();
builder.Services.AddScoped<CsvPlotterStateService>();
builder.Services.AddScoped<CsvExportService>();
builder.Services.AddScoped<StatisticsService>();
builder.Services.AddScoped<FileHistoryService>();
builder.Services.AddScoped<SessionStorageService>();
builder.Services.AddScoped<UserProfileService>();
builder.Services.AddSingleton<TwoFactorService>();
builder.Services.AddScoped<SecureFormulaEvaluator>();
builder.Services.AddScoped<FileUploadValidator>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<SessionSharingService>();
builder.Services.AddScoped<CommentService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<NotificationDispatcher>();
builder.Services.AddScoped<ApiKeyService>();
builder.Services.AddScoped<WebhookService>();
builder.Services.AddScoped<WebhookDeliveryService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<TrendAnalysisService>();
builder.Services.AddScoped<ForecastingService>();
builder.Services.AddScoped<ScheduledReportService>();
builder.Services.AddScoped<StatisticalAnalysisService>();

var app = builder.Build();
// Keep local data private even if hosting settings are changed.
app.Use(async (context, next) =>
{
    var remote = context.Connection.RemoteIpAddress;
    if (remote is not null && !System.Net.IPAddress.IsLoopback(remote))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    await next(context);
});
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok", storage = "sqlite", externalDatabase = false
}));
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DataManagementDbContext>>();
    using var database = factory.CreateDbContext();
    database.Database.EnsureCreated();
    await scope.ServiceProvider.GetRequiredService<UserProfileService>()
        .GetOrCreateProfileAsync(LocalProfileService.LocalUserId, "local@localhost");
}
app.Run();
public partial class Program { }
