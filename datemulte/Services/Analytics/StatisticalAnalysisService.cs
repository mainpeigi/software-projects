using Datemulte_2.Models;
using Datemulte_2.Services.DataManagement;

namespace Datemulte_2.Services.Analytics;

/// <summary>
/// Provides advanced statistical analysis including percentiles, distributions, and hypothesis testing
/// </summary>
public class StatisticalAnalysisService
{
    private readonly DataSessionService _sessionService;
    private readonly ILogger<StatisticalAnalysisService> _logger;

    public StatisticalAnalysisService(
        DataSessionService sessionService,
        ILogger<StatisticalAnalysisService> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>
    /// Calculate descriptive statistics for a column
    /// </summary>
    public async Task<DescriptiveStatistics> CalculateDescriptiveStatisticsAsync(
        Guid sessionId,
        string columnName,
        Guid userId)
    {
        var (session, data) = await _sessionService.LoadSessionAsync(sessionId, userId);

        var values = data
            .Select(row => row.GetNumericValue(columnName))
            .Where(val => !double.IsNaN(val) && !double.IsInfinity(val))
            .OrderBy(v => v)
            .ToList();

        if (values.Count == 0)
        {
            return new DescriptiveStatistics { ColumnName = columnName };
        }

        var mean = values.Average();
        var median = CalculateMedian(values);
        var mode = CalculateMode(values);
        var stdDev = CalculateStandardDeviation(values, mean);
        var variance = Math.Pow(stdDev, 2);
        var range = values.Max() - values.Min();
        var q1 = CalculatePercentile(values, 25);
        var q3 = CalculatePercentile(values, 75);
        var iqr = q3 - q1;
        var skewness = CalculateSkewness(values, mean, stdDev);
        var kurtosis = CalculateKurtosis(values, mean, stdDev);

        return new DescriptiveStatistics
        {
            ColumnName = columnName,
            Count = values.Count,
            Mean = mean,
            Median = median,
            Mode = mode,
            StandardDeviation = stdDev,
            Variance = variance,
            Minimum = values.Min(),
            Maximum = values.Max(),
            Range = range,
            Q1 = q1,
            Q3 = q3,
            IQR = iqr,
            Skewness = skewness,
            Kurtosis = kurtosis
        };
    }

    /// <summary>
    /// Calculate percentiles for a column
    /// </summary>
    public async Task<PercentileResult> CalculatePercentilesAsync(
        Guid sessionId,
        string columnName,
        Guid userId,
        List<double> percentiles)
    {
        var (session, data) = await _sessionService.LoadSessionAsync(sessionId, userId);

        var values = data
            .Select(row => row.GetNumericValue(columnName))
            .Where(val => !double.IsNaN(val) && !double.IsInfinity(val))
            .OrderBy(v => v)
            .ToList();

        var result = new PercentileResult
        {
            ColumnName = columnName,
            Count = values.Count
        };

        foreach (var p in percentiles)
        {
            var value = CalculatePercentile(values, p);
            result.Percentiles[p] = value;
        }

        return result;
    }

    /// <summary>
    /// Detect outliers using IQR method
    /// </summary>
    public async Task<OutlierDetectionResult> DetectOutliersAsync(
        Guid sessionId,
        string columnName,
        Guid userId,
        double iqrMultiplier = 1.5)
    {
        var (session, data) = await _sessionService.LoadSessionAsync(sessionId, userId);

        var values = data
            .Select(row => row.GetNumericValue(columnName))
            .Where(val => !double.IsNaN(val) && !double.IsInfinity(val))
            .ToList();

        var sortedValues = values.OrderBy(v => v).ToList();
        var q1 = CalculatePercentile(sortedValues, 25);
        var q3 = CalculatePercentile(sortedValues, 75);
        var iqr = q3 - q1;

        var lowerBound = q1 - (iqrMultiplier * iqr);
        var upperBound = q3 + (iqrMultiplier * iqr);

        var outliers = new List<OutlierPoint>();
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] < lowerBound || values[i] > upperBound)
            {
                outliers.Add(new OutlierPoint
                {
                    Index = i,
                    Value = values[i],
                    IsHigh = values[i] > upperBound
                });
            }
        }

        return new OutlierDetectionResult
        {
            ColumnName = columnName,
            Q1 = q1,
            Q3 = q3,
            IQR = iqr,
            LowerBound = lowerBound,
            UpperBound = upperBound,
            Outliers = outliers,
            OutlierPercentage = (outliers.Count / (double)values.Count) * 100
        };
    }

    /// <summary>
    /// Calculate histogram bins for distribution visualization
    /// </summary>
    public async Task<HistogramResult> CalculateHistogramAsync(
        Guid sessionId,
        string columnName,
        Guid userId,
        int binCount = 10)
    {
        var (session, data) = await _sessionService.LoadSessionAsync(sessionId, userId);

        var values = data
            .Select(row => row.GetNumericValue(columnName))
            .Where(val => !double.IsNaN(val) && !double.IsInfinity(val))
            .ToList();

        if (values.Count == 0)
        {
            return new HistogramResult { ColumnName = columnName };
        }

        var min = values.Min();
        var max = values.Max();
        var binWidth = (max - min) / binCount;

        var bins = new List<HistogramBin>();
        for (int i = 0; i < binCount; i++)
        {
            var binMin = min + (i * binWidth);
            var binMax = i == binCount - 1 ? max : binMin + binWidth;

            var count = values.Count(v => v >= binMin && v <= binMax);

            bins.Add(new HistogramBin
            {
                BinStart = binMin,
                BinEnd = binMax,
                Count = count,
                Frequency = count / (double)values.Count
            });
        }

        return new HistogramResult
        {
            ColumnName = columnName,
            Bins = bins,
            TotalCount = values.Count
        };
    }

    private double CalculateMedian(List<double> sortedValues)
    {
        int n = sortedValues.Count;
        if (n % 2 == 0)
            return (sortedValues[n / 2 - 1] + sortedValues[n / 2]) / 2.0;
        return sortedValues[n / 2];
    }

    private double? CalculateMode(List<double> values)
    {
        var groups = values.GroupBy(v => Math.Round(v, 2))
            .OrderByDescending(g => g.Count())
            .ToList();

        if (groups.Count == 0 || groups[0].Count() == 1)
            return null;

        return groups[0].Key;
    }

    private double CalculatePercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
            return 0;

        double n = sortedValues.Count;
        double index = (percentile / 100.0) * (n - 1);
        int lowerIndex = (int)Math.Floor(index);
        int upperIndex = (int)Math.Ceiling(index);

        if (lowerIndex == upperIndex)
            return sortedValues[lowerIndex];

        double fraction = index - lowerIndex;
        return sortedValues[lowerIndex] + fraction * (sortedValues[upperIndex] - sortedValues[lowerIndex]);
    }

    private double CalculateStandardDeviation(List<double> values, double mean)
    {
        var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
        return Math.Sqrt(variance);
    }

    private double CalculateSkewness(List<double> values, double mean, double stdDev)
    {
        if (stdDev == 0)
            return 0;

        var n = values.Count;
        var sum = values.Sum(v => Math.Pow((v - mean) / stdDev, 3));
        return (n * sum) / ((n - 1) * (n - 2));
    }

    private double CalculateKurtosis(List<double> values, double mean, double stdDev)
    {
        if (stdDev == 0)
            return 0;

        var n = values.Count;
        var sum = values.Sum(v => Math.Pow((v - mean) / stdDev, 4));
        return (n * (n + 1) * sum) / ((n - 1) * (n - 2) * (n - 3)) - (3 * Math.Pow(n - 1, 2)) / ((n - 2) * (n - 3));
    }
}

public class DescriptiveStatistics
{
    public string ColumnName { get; set; } = "";
    public int Count { get; set; }
    public double Mean { get; set; }
    public double Median { get; set; }
    public double? Mode { get; set; }
    public double StandardDeviation { get; set; }
    public double Variance { get; set; }
    public double Minimum { get; set; }
    public double Maximum { get; set; }
    public double Range { get; set; }
    public double Q1 { get; set; }
    public double Q3 { get; set; }
    public double IQR { get; set; }
    public double Skewness { get; set; }
    public double Kurtosis { get; set; }
}

public class PercentileResult
{
    public string ColumnName { get; set; } = "";
    public int Count { get; set; }
    public Dictionary<double, double> Percentiles { get; set; } = new();
}

public class OutlierDetectionResult
{
    public string ColumnName { get; set; } = "";
    public double Q1 { get; set; }
    public double Q3 { get; set; }
    public double IQR { get; set; }
    public double LowerBound { get; set; }
    public double UpperBound { get; set; }
    public List<OutlierPoint> Outliers { get; set; } = new();
    public double OutlierPercentage { get; set; }
}

public class OutlierPoint
{
    public int Index { get; set; }
    public double Value { get; set; }
    public bool IsHigh { get; set; }
}

public class HistogramResult
{
    public string ColumnName { get; set; } = "";
    public List<HistogramBin> Bins { get; set; } = new();
    public int TotalCount { get; set; }
}

public class HistogramBin
{
    public double BinStart { get; set; }
    public double BinEnd { get; set; }
    public int Count { get; set; }
    public double Frequency { get; set; }
}
