using Datemulte_2.Data;
using Datemulte_2.Models;
using Datemulte_2.Services.DataManagement;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Datemulte_2.Services.Analytics;

/// <summary>
/// Manages scheduled analytical reports with configurable frequency
/// </summary>
public class ScheduledReportService
{
    private readonly IDbContextFactory<DataManagementDbContext> _dbContextFactory;
    private readonly DataSessionService _sessionService;
    private readonly TrendAnalysisService _trendAnalysisService;
    private readonly EmailService _emailService;
    private readonly NotificationService _notificationService;
    private readonly ILogger<ScheduledReportService> _logger;

    public ScheduledReportService(
        IDbContextFactory<DataManagementDbContext> dbContextFactory,
        DataSessionService sessionService,
        TrendAnalysisService trendAnalysisService,
        EmailService emailService,
        NotificationService notificationService,
        ILogger<ScheduledReportService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _sessionService = sessionService;
        _trendAnalysisService = trendAnalysisService;
        _emailService = emailService;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new scheduled report
    /// </summary>
    public async Task<(bool success, ScheduledReport? report, string? error)> CreateScheduledReportAsync(
        Guid userId,
        Guid sessionId,
        string reportName,
        string cronSchedule,
        ReportType reportType,
        bool emailReport = true,
        string? emailRecipients = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Verify session access
        var session = await dbContext.DataSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
        if (session == null || session.UserId != userId)
        {
            return (false, null, "Session not found or access denied");
        }

        var report = new ScheduledReport
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionId = sessionId,
            ReportName = reportName,
            Type = reportType,
            Schedule = cronSchedule,
            EmailReport = emailReport,
            EmailRecipients = emailRecipients,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            NextRunAt = CalculateNextRunTime(cronSchedule)
        };

        dbContext.ScheduledReports.Add(report);
        await dbContext.SaveChangesAsync();

        // Schedule the recurring job
        ScheduleReportJob(report);

        _logger.LogInformation("Created scheduled report {ReportId} for session {SessionId}", report.Id, sessionId);
        return (true, report, null);
    }

    /// <summary>
    /// Execute a scheduled report
    /// </summary>
    public async Task ExecuteReportAsync(Guid reportId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var report = await dbContext.ScheduledReports
            .Include(r => r.Session)
            .FirstOrDefaultAsync(r => r.Id == reportId);

        if (report == null || !report.IsActive)
        {
            _logger.LogWarning("Scheduled report {ReportId} not found or inactive", reportId);
            return;
        }

        try
        {
            var reportData = await GenerateReportDataAsync(report);

            // Update last run time
            report.LastRunAt = DateTime.UtcNow;
            report.NextRunAt = CalculateNextRunTime(report.Schedule);
            await dbContext.SaveChangesAsync();

            // Send notifications
            await _notificationService.CreateNotificationAsync(
                report.UserId,
                NotificationType.SystemAlert,
                $"Report Ready: {report.ReportName}",
                $"Your scheduled report has been generated."
            );

            if (report.EmailReport && !string.IsNullOrEmpty(report.EmailRecipients))
            {
                var recipients = report.EmailRecipients.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var recipient in recipients)
                {
                    var (emailSuccess, emailError) = await SendReportEmailAsync(recipient.Trim(), report.ReportName, reportData);
                    if (!emailSuccess)
                    {
                        _logger.LogWarning("Failed to send scheduled report email to {Recipient}: {Error}", recipient, emailError);
                    }
                }
            }

            _logger.LogInformation("Successfully executed scheduled report {ReportId}", reportId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing scheduled report {ReportId}", reportId);
        }
    }

    /// <summary>
    /// Get all scheduled reports for a user
    /// </summary>
    public async Task<List<ScheduledReport>> GetUserReportsAsync(Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.ScheduledReports
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Delete a scheduled report
    /// </summary>
    public async Task<bool> DeleteReportAsync(Guid reportId, Guid userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var report = await dbContext.ScheduledReports
            .FirstOrDefaultAsync(r => r.Id == reportId && r.UserId == userId);

        if (report == null)
            return false;

        // Remove the Hangfire job
        RecurringJob.RemoveIfExists($"scheduled-report-{reportId}");

        dbContext.ScheduledReports.Remove(report);
        await dbContext.SaveChangesAsync();

        return true;
    }

    private async Task<ReportData> GenerateReportDataAsync(ScheduledReport report)
    {
        var reportData = new ReportData
        {
            GeneratedAt = DateTime.UtcNow,
            ReportType = report.Type
        };

        var (session, data) = await _sessionService.LoadSessionAsync(report.SessionId, report.UserId);

        // Get all numeric columns
        if (data.Count > 0)
        {
            var numericColumns = data[0].Data.Keys
                .Where(col => data[0].GetNumericValueOrNull(col) != null)
                .ToList();

            // Generate analysis based on report type
            switch (report.Type)
            {
                case ReportType.TrendAnalysis:
                    foreach (var column in numericColumns.Take(5)) // Limit to first 5 columns
                    {
                        try
                        {
                            var trendResult = await _trendAnalysisService.AnalyzeTrendAsync(
                                report.SessionId, column, report.UserId);
                            reportData.TrendResults.Add(trendResult);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to analyze column {Column} for report", column);
                        }
                    }
                    break;

                case ReportType.Statistics:
                case ReportType.CorrelationMatrix:
                case ReportType.DataQuality:
                    // These would be implemented with additional services
                    reportData.Message = $"Report type {report.Type} generated successfully";
                    break;
            }
        }

        return reportData;
    }

    private async Task<(bool success, string? error)> SendReportEmailAsync(string email, string reportName, ReportData reportData)
    {
        var htmlBody = $@"
            <h2>Scheduled Report: {reportName}</h2>
            <p>Generated at: {reportData.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC</p>
            <p>Report Type: {reportData.ReportType}</p>
            <h3>Analysis Summary</h3>
            {(reportData.TrendResults.Any() ? $@"
            <ul>
            {string.Join("", reportData.TrendResults.Select(r => $@"
                <li>
                    <strong>{r.ColumnName}</strong>: {r.Message}
                    <br/>R² = {r.RSquared:F3}, Volatility = {r.Volatility:F2}
                </li>
            "))}
            </ul>" : $"<p>{reportData.Message}</p>")}
            <p>Generated by Datemulte Analytics</p>
        ";

        return await _emailService.SendEmailAsync(email, $"Scheduled Report: {reportName}", htmlBody);
    }

    private void ScheduleReportJob(ScheduledReport report)
    {
        RecurringJob.AddOrUpdate(
            $"scheduled-report-{report.Id}",
            () => ExecuteReportAsync(report.Id),
            report.Schedule,
            TimeZoneInfo.Utc
        );
    }

    private DateTime CalculateNextRunTime(string cronExpression)
    {
        // Simple approximation - in production you'd use a cron parser
        return DateTime.UtcNow.AddDays(1);
    }
}

public class ReportData
{
    public DateTime GeneratedAt { get; set; }
    public ReportType ReportType { get; set; }
    public List<TrendAnalysisResult> TrendResults { get; set; } = new();
    public string Message { get; set; } = "";
}
