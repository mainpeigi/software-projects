using Datemulte_2.Models;
using Datemulte_2.Services.DataManagement;

namespace Datemulte_2.Services.Analytics;

/// <summary>
/// Detects trends in time-series data using linear regression and moving averages
/// </summary>
public class TrendAnalysisService
{
    private readonly DataSessionService _sessionService;
    private readonly ILogger<TrendAnalysisService> _logger;

    public TrendAnalysisService(
        DataSessionService sessionService,
        ILogger<TrendAnalysisService> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>
    /// Analyze trends for a specific column in a session
    /// </summary>
    public async Task<TrendAnalysisResult> AnalyzeTrendAsync(
        Guid sessionId,
        string columnName,
        Guid userId,
        int windowSize = 7)
    {
        var (session, data) = await _sessionService.LoadSessionAsync(sessionId, userId);

        var values = ExtractNumericValues(data, columnName);

        if (values.Count < 2)
        {
            return new TrendAnalysisResult
            {
                ColumnName = columnName,
                TrendDirection = TrendDirection.Insufficient,
                Message = "Insufficient data points for trend analysis"
            };
        }

        var linearTrend = CalculateLinearTrend(values);
        var movingAverage = CalculateMovingAverage(values, windowSize);
        var volatility = CalculateVolatility(values);

        return new TrendAnalysisResult
        {
            ColumnName = columnName,
            TrendDirection = DetermineTrendDirection(linearTrend.Slope),
            Slope = linearTrend.Slope,
            Intercept = linearTrend.Intercept,
            RSquared = linearTrend.RSquared,
            MovingAverage = movingAverage,
            Volatility = volatility,
            DataPoints = values.Count,
            Message = GenerateTrendMessage(linearTrend.Slope, volatility)
        };
    }

    /// <summary>
    /// Detect anomalies using standard deviation method
    /// </summary>
    public async Task<List<AnomalyDetection>> DetectAnomaliesAsync(
        Guid sessionId,
        string columnName,
        Guid userId,
        double threshold = 2.0)
    {
        var (session, data) = await _sessionService.LoadSessionAsync(sessionId, userId);

        var values = ExtractNumericValues(data, columnName);
        var anomalies = new List<AnomalyDetection>();

        if (values.Count < 3)
            return anomalies;

        var mean = values.Average();
        var stdDev = CalculateStandardDeviation(values, mean);

        for (int i = 0; i < values.Count; i++)
        {
            var zScore = Math.Abs((values[i] - mean) / stdDev);

            if (zScore > threshold)
            {
                anomalies.Add(new AnomalyDetection
                {
                    Index = i,
                    Value = values[i],
                    ZScore = zScore,
                    DeviationFromMean = values[i] - mean,
                    Severity = zScore > 3 ? AnomalySeverity.High : AnomalySeverity.Medium
                });
            }
        }

        _logger.LogInformation("Detected {Count} anomalies in column {Column}", anomalies.Count, columnName);
        return anomalies;
    }

    /// <summary>
    /// Calculate correlation between two columns
    /// </summary>
    public async Task<CorrelationResult> CalculateCorrelationAsync(
        Guid sessionId,
        string columnX,
        string columnY,
        Guid userId)
    {
        var (session, data) = await _sessionService.LoadSessionAsync(sessionId, userId);

        var valuesX = ExtractNumericValues(data, columnX);
        var valuesY = ExtractNumericValues(data, columnY);

        if (valuesX.Count != valuesY.Count || valuesX.Count < 2)
        {
            return new CorrelationResult
            {
                ColumnX = columnX,
                ColumnY = columnY,
                Message = "Insufficient or mismatched data for correlation analysis"
            };
        }

        var correlation = CalculatePearsonCorrelation(valuesX, valuesY);

        return new CorrelationResult
        {
            ColumnX = columnX,
            ColumnY = columnY,
            Correlation = correlation,
            Strength = DetermineCorrelationStrength(correlation),
            DataPoints = valuesX.Count,
            Message = GenerateCorrelationMessage(correlation)
        };
    }

    private List<double> ExtractNumericValues(List<GenericCsvRow> data, string columnName)
    {
        return data
            .Select(row => row.GetNumericValue(columnName))
            .Where(val => !double.IsNaN(val) && !double.IsInfinity(val))
            .ToList();
    }

    private (double Slope, double Intercept, double RSquared) CalculateLinearTrend(List<double> values)
    {
        int n = values.Count;
        var indices = Enumerable.Range(0, n).Select(i => (double)i).ToList();

        var meanX = indices.Average();
        var meanY = values.Average();

        var numerator = 0.0;
        var denominatorX = 0.0;
        var denominatorY = 0.0;

        for (int i = 0; i < n; i++)
        {
            var dx = indices[i] - meanX;
            var dy = values[i] - meanY;
            numerator += dx * dy;
            denominatorX += dx * dx;
            denominatorY += dy * dy;
        }

        var slope = numerator / denominatorX;
        var intercept = meanY - slope * meanX;
        var rSquared = Math.Pow(numerator, 2) / (denominatorX * denominatorY);

        return (slope, intercept, rSquared);
    }

    private List<double> CalculateMovingAverage(List<double> values, int windowSize)
    {
        var movingAvg = new List<double>();

        for (int i = 0; i < values.Count; i++)
        {
            int start = Math.Max(0, i - windowSize + 1);
            int count = i - start + 1;
            var avg = values.Skip(start).Take(count).Average();
            movingAvg.Add(avg);
        }

        return movingAvg;
    }

    private double CalculateVolatility(List<double> values)
    {
        if (values.Count < 2)
            return 0;

        var mean = values.Average();
        return CalculateStandardDeviation(values, mean);
    }

    private double CalculateStandardDeviation(List<double> values, double mean)
    {
        var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
        return Math.Sqrt(variance);
    }

    private double CalculatePearsonCorrelation(List<double> x, List<double> y)
    {
        int n = x.Count;
        var meanX = x.Average();
        var meanY = y.Average();

        var numerator = 0.0;
        var denominatorX = 0.0;
        var denominatorY = 0.0;

        for (int i = 0; i < n; i++)
        {
            var dx = x[i] - meanX;
            var dy = y[i] - meanY;
            numerator += dx * dy;
            denominatorX += dx * dx;
            denominatorY += dy * dy;
        }

        if (denominatorX == 0 || denominatorY == 0)
            return 0;

        return numerator / Math.Sqrt(denominatorX * denominatorY);
    }

    private TrendDirection DetermineTrendDirection(double slope)
    {
        if (Math.Abs(slope) < 0.001)
            return TrendDirection.Flat;
        return slope > 0 ? TrendDirection.Increasing : TrendDirection.Decreasing;
    }

    private CorrelationStrength DetermineCorrelationStrength(double correlation)
    {
        var abs = Math.Abs(correlation);
        if (abs >= 0.7) return CorrelationStrength.Strong;
        if (abs >= 0.4) return CorrelationStrength.Moderate;
        if (abs >= 0.2) return CorrelationStrength.Weak;
        return CorrelationStrength.None;
    }

    private string GenerateTrendMessage(double slope, double volatility)
    {
        var direction = slope > 0 ? "increasing" : slope < 0 ? "decreasing" : "stable";
        var stability = volatility < 1 ? "low volatility" : volatility < 5 ? "moderate volatility" : "high volatility";
        return $"Trend is {direction} with {stability}";
    }

    private string GenerateCorrelationMessage(double correlation)
    {
        var abs = Math.Abs(correlation);
        var strength = abs >= 0.7 ? "strong" : abs >= 0.4 ? "moderate" : abs >= 0.2 ? "weak" : "no";
        var direction = correlation > 0 ? "positive" : "negative";
        return $"Shows {strength} {direction} correlation";
    }
}

public class TrendAnalysisResult
{
    public string ColumnName { get; set; } = "";
    public TrendDirection TrendDirection { get; set; }
    public double Slope { get; set; }
    public double Intercept { get; set; }
    public double RSquared { get; set; }
    public List<double> MovingAverage { get; set; } = new();
    public double Volatility { get; set; }
    public int DataPoints { get; set; }
    public string Message { get; set; } = "";
}

public class AnomalyDetection
{
    public int Index { get; set; }
    public double Value { get; set; }
    public double ZScore { get; set; }
    public double DeviationFromMean { get; set; }
    public AnomalySeverity Severity { get; set; }
}

public class CorrelationResult
{
    public string ColumnX { get; set; } = "";
    public string ColumnY { get; set; } = "";
    public double Correlation { get; set; }
    public CorrelationStrength Strength { get; set; }
    public int DataPoints { get; set; }
    public string Message { get; set; } = "";
}

public enum TrendDirection
{
    Increasing,
    Decreasing,
    Flat,
    Insufficient
}

public enum AnomalySeverity
{
    Low,
    Medium,
    High
}

public enum CorrelationStrength
{
    None,
    Weak,
    Moderate,
    Strong
}
