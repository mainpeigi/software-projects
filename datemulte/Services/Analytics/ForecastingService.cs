using Datemulte_2.Models;
using Datemulte_2.Services.DataManagement;

namespace Datemulte_2.Services.Analytics;

/// <summary>
/// Provides forecasting capabilities using simple exponential smoothing and linear extrapolation
/// </summary>
public class ForecastingService
{
    private readonly DataSessionService _sessionService;
    private readonly TrendAnalysisService _trendAnalysisService;
    private readonly ILogger<ForecastingService> _logger;

    public ForecastingService(
        DataSessionService sessionService,
        TrendAnalysisService trendAnalysisService,
        ILogger<ForecastingService> logger)
    {
        _sessionService = sessionService;
        _trendAnalysisService = trendAnalysisService;
        _logger = logger;
    }

    /// <summary>
    /// Generate forecast using linear extrapolation
    /// </summary>
    public async Task<ForecastResult> GenerateLinearForecastAsync(
        Guid sessionId,
        string columnName,
        Guid userId,
        int periodsAhead = 10)
    {
        var trendResult = await _trendAnalysisService.AnalyzeTrendAsync(sessionId, columnName, userId);

        if (trendResult.DataPoints < 2)
        {
            return new ForecastResult
            {
                ColumnName = columnName,
                Method = "Linear",
                Message = "Insufficient data for forecasting"
            };
        }

        var forecasts = new List<ForecastPoint>();
        var lastIndex = trendResult.DataPoints - 1;

        for (int i = 1; i <= periodsAhead; i++)
        {
            var futureIndex = lastIndex + i;
            var predictedValue = trendResult.Intercept + (trendResult.Slope * futureIndex);

            forecasts.Add(new ForecastPoint
            {
                Period = i,
                PredictedValue = predictedValue,
                ConfidenceLower = predictedValue - (trendResult.Volatility * 2),
                ConfidenceUpper = predictedValue + (trendResult.Volatility * 2)
            });
        }

        return new ForecastResult
        {
            ColumnName = columnName,
            Method = "Linear Extrapolation",
            Forecasts = forecasts,
            BaselineVolatility = trendResult.Volatility,
            RSquared = trendResult.RSquared,
            Message = $"Generated {periodsAhead} period forecast with R²={trendResult.RSquared:F3}"
        };
    }

    /// <summary>
    /// Generate forecast using exponential smoothing
    /// </summary>
    public async Task<ForecastResult> GenerateExponentialSmoothingForecastAsync(
        Guid sessionId,
        string columnName,
        Guid userId,
        int periodsAhead = 10,
        double alpha = 0.3)
    {
        var (session, data) = await _sessionService.LoadSessionAsync(sessionId, userId);

        var values = data
            .Select(row => row.GetNumericValue(columnName))
            .Where(val => !double.IsNaN(val) && !double.IsInfinity(val))
            .ToList();

        if (values.Count < 2)
        {
            return new ForecastResult
            {
                ColumnName = columnName,
                Method = "Exponential Smoothing",
                Message = "Insufficient data for forecasting"
            };
        }

        // Calculate smoothed values
        var smoothed = new List<double> { values[0] };
        for (int i = 1; i < values.Count; i++)
        {
            var smoothedValue = alpha * values[i] + (1 - alpha) * smoothed[i - 1];
            smoothed.Add(smoothedValue);
        }

        // Calculate residuals for confidence intervals
        var residuals = values.Zip(smoothed, (actual, predicted) => actual - predicted).ToList();
        var residualStdDev = CalculateStandardDeviation(residuals);

        // Generate forecasts
        var forecasts = new List<ForecastPoint>();
        var lastSmoothed = smoothed.Last();

        for (int i = 1; i <= periodsAhead; i++)
        {
            forecasts.Add(new ForecastPoint
            {
                Period = i,
                PredictedValue = lastSmoothed,
                ConfidenceLower = lastSmoothed - (residualStdDev * 2),
                ConfidenceUpper = lastSmoothed + (residualStdDev * 2)
            });
        }

        return new ForecastResult
        {
            ColumnName = columnName,
            Method = $"Exponential Smoothing (α={alpha})",
            Forecasts = forecasts,
            BaselineVolatility = residualStdDev,
            Message = $"Generated {periodsAhead} period forecast using exponential smoothing"
        };
    }

    private double CalculateStandardDeviation(List<double> values)
    {
        if (values.Count < 2)
            return 0;

        var mean = values.Average();
        var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
        return Math.Sqrt(variance);
    }
}

public class ForecastResult
{
    public string ColumnName { get; set; } = "";
    public string Method { get; set; } = "";
    public List<ForecastPoint> Forecasts { get; set; } = new();
    public double BaselineVolatility { get; set; }
    public double RSquared { get; set; }
    public string Message { get; set; } = "";
}

public class ForecastPoint
{
    public int Period { get; set; }
    public double PredictedValue { get; set; }
    public double ConfidenceLower { get; set; }
    public double ConfidenceUpper { get; set; }
}
