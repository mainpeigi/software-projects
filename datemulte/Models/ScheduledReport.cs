using Datemulte_2.Models.DataManagement;

namespace Datemulte_2.Models;

/// <summary>
/// Scheduled analytics report configuration
/// Generates and emails reports on a recurring schedule
/// </summary>
public class ScheduledReport
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SessionId { get; set; }
    
    // Report configuration
    public string ReportName { get; set; } = "";
    public ReportType Type { get; set; }
    
    // Schedule (cron expression: "0 0 * * 1" = weekly on Monday)
    public string Schedule { get; set; } = "";
    public bool IsActive { get; set; } = true;
    
    // Execution tracking
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    
    // Delivery options
    public bool EmailReport { get; set; } = false;
    public string? EmailRecipients { get; set; }  // Comma-separated email addresses
    
    // Navigation
    public UserProfile User { get; set; } = null!;
    public DataSession Session { get; set; } = null!;
}

/// <summary>
/// Types of reports that can be generated
/// </summary>
public enum ReportType
{
    Statistics,         // Basic statistical analysis
    TrendAnalysis,      // Trend detection and forecasting
    CorrelationMatrix,  // Correlation between columns
    DataQuality         // Data quality scorecard
}
