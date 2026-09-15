using Datemulte_2.Models;

namespace Datemulte_2.Services;

/// <summary>
/// Service for calculating statistical metrics on dataset columns
/// Provides descriptive statistics, correlation analysis, and outlier detection
/// Supports parallel processing for multi-column analysis
/// </summary>
public class StatisticsService
{
    /// <summary>
    /// Calculates comprehensive statistics for a single column
    /// Includes: min, max, mean, median, std dev, percentiles, outliers, and moving average
    /// </summary>
    /// <param name="data">Dataset rows</param>
    /// <param name="columnName">Column to analyze</param>
    /// <param name="movingAverageWindow">Window size for moving average calculation</param>
    /// <returns>ColumnStatistics object with all calculated metrics</returns>
    public ColumnStatistics CalculateStatistics(List<GenericCsvRow> data, string columnName, int movingAverageWindow = 10)
    {
        // Extract numeric values from the column, filtering out null/invalid entries
        var values = data.Select(row => row.GetNumericValueOrNull(columnName))
            .Where(v => v != null)
            .Select(v => (double)v!)
            .ToList();

        // Return empty statistics if no valid values
        if (values.Count == 0)
        {
            return new ColumnStatistics { ColumnName = columnName };
        }

        // Sort values once for percentile calculations
        var sortedValues = values.OrderBy(v => v).ToList();
        
        var stats = new ColumnStatistics
        {
            ColumnName = columnName,
            Count = values.Count,
            Min = sortedValues.First(),
            Max = sortedValues.Last(),
            Mean = values.Average(),
            Sum = values.Sum()
        };
        
        stats.Range = stats.Max - stats.Min;
        stats.Median = CalculatePercentile(sortedValues, 50);
        stats.Percentile25 = CalculatePercentile(sortedValues, 25);
        stats.Percentile75 = CalculatePercentile(sortedValues, 75);
        stats.Percentile95 = CalculatePercentile(sortedValues, 95);
        
        stats.Variance = values.Sum(v => Math.Pow(v - stats.Mean, 2)) / values.Count;
        stats.StandardDeviation = Math.Sqrt(stats.Variance);
        
        var iqr = stats.Percentile75 - stats.Percentile25;
        var lowerBound = stats.Percentile25 - 1.5 * iqr;
        var upperBound = stats.Percentile75 + 1.5 * iqr;
        stats.OutlierCount = values.Count(v => v < lowerBound || v > upperBound);
        stats.IQR = iqr;
        stats.LowerOutlierBound = lowerBound;
        stats.UpperOutlierBound = upperBound;
        
        if (values.Count >= movingAverageWindow)
        {
            stats.MovingAverage = CalculateMovingAverage(values, movingAverageWindow);
        }
        
        return stats;
    }
    
    /// <summary>
    /// Calculates statistics for multiple columns in parallel using thread-safe concurrent dictionary
    /// Improves performance when analyzing large datasets with many columns
    /// </summary>
    /// <param name="data">Dataset rows</param>
    /// <param name="columnNames">Columns to analyze</param>
    /// <param name="movingAverageWindow">Window size for moving average calculation</param>
    /// <returns>Dictionary mapping column names to their statistics</returns>
    public Dictionary<string, ColumnStatistics> CalculateMultipleStatistics(List<GenericCsvRow> data, IEnumerable<string> columnNames, int movingAverageWindow = 10)
    {
        // Use thread-safe dictionary for parallel processing
        var results = new System.Collections.Concurrent.ConcurrentDictionary<string, ColumnStatistics>();

        // Process each column in parallel for better performance
        Parallel.ForEach(columnNames, columnName =>
        {
            var stats = CalculateStatistics(data, columnName, movingAverageWindow);
            results[columnName] = stats;
        });

        return new Dictionary<string, ColumnStatistics>(results);
    }

    /// <summary>
    /// Calculates Pearson correlation matrix for multiple columns
    /// Returns correlation coefficients between -1 (perfect negative) and +1 (perfect positive)
    /// Diagonal values are always 1.0 (perfect self-correlation)
    /// </summary>
    /// <param name="data">Dataset rows</param>
    /// <param name="columnNames">Columns to correlate</param>
    /// <returns>2D dictionary matrix of correlation coefficients</returns>
    public Dictionary<string, Dictionary<string, double>> CalculateCorrelationMatrix(List<GenericCsvRow> data, IEnumerable<string> columnNames)
    {
        var columns = columnNames.ToList();
        var matrix = new Dictionary<string, Dictionary<string, double>>();

        // Build correlation matrix for all column pairs
        foreach (var col1 in columns)
        {
            matrix[col1] = new Dictionary<string, double>();

            foreach (var col2 in columns)
            {
                // Self-correlation is always 1.0
                if (col1 == col2)
                {
                    matrix[col1][col2] = 1.0;
                }
                else
                {
                    // Extract valid paired values (filter out nulls)
                    var pairedValues = data
                        .Select(row => new {
                            v1 = row.GetNumericValueOrNull(col1),
                            v2 = row.GetNumericValueOrNull(col2)
                        })
                        .Where(pair => pair.v1 != null && pair.v2 != null)
                        .ToList();

                    var values1 = pairedValues.Select(p => (double)p.v1!).ToList();
                    var values2 = pairedValues.Select(p => (double)p.v2!).ToList();

                    matrix[col1][col2] = CalculatePearsonCorrelation(values1, values2);
                }
            }
        }
        return matrix;
    }

    /// <summary>
    /// Calculates percentile value using linear interpolation method
    /// Expects pre-sorted values for efficiency
    /// </summary>
    /// <param name="sortedValues">Pre-sorted list of values</param>
    /// <param name="percentile">Percentile to calculate (0-100)</param>
    /// <returns>Interpolated percentile value</returns>
    private double CalculatePercentile(List<double> sortedValues, double percentile)
    {
        // Handle edge cases
        if (sortedValues.Count == 0) return 0;
        if (sortedValues.Count == 1) return sortedValues[0];

        // Calculate fractional index position
        double index = (percentile / 100.0) * (sortedValues.Count - 1);
        int lowerIndex = (int)Math.Floor(index);
        int upperIndex = (int)Math.Ceiling(index);

        // Exact match - no interpolation needed
        if (lowerIndex == upperIndex)
        {
            return sortedValues[lowerIndex];
        }

        // Linear interpolation between adjacent values
        double lowerValue = sortedValues[lowerIndex];
        double upperValue = sortedValues[upperIndex];
        double fraction = index - lowerIndex;

        return lowerValue + (upperValue - lowerValue) * fraction;
    }

    /// <summary>
    /// Calculates simple moving average of the last N values
    /// Used for trend analysis and smoothing noisy data
    /// </summary>
    /// <param name="values">List of values</param>
    /// <param name="window">Number of values to average</param>
    /// <returns>Average of last N values, or 0 if insufficient data</returns>
    private double CalculateMovingAverage(List<double> values, int window)
    {
        // Require sufficient data points for the window
        if (values.Count < window) return 0;

        return values.TakeLast(window).Average();
    }

    /// <summary>
    /// Calculates Pearson correlation coefficient between two variables
    /// Measures linear relationship strength: -1 (perfect negative) to +1 (perfect positive), 0 (no correlation)
    /// Formula: r = Σ[(xi - x̄)(yi - ȳ)] / √[Σ(xi - x̄)² × Σ(yi - ȳ)²]
    /// </summary>
    /// <param name="x">First variable values</param>
    /// <param name="y">Second variable values</param>
    /// <returns>Correlation coefficient between -1 and +1, or 0 if invalid</returns>
    private double CalculatePearsonCorrelation(List<double> x, List<double> y)
    {
        // Validate inputs - arrays must be same length and non-empty
        if (x.Count != y.Count || x.Count == 0) return 0;

        double meanX = x.Average();
        double meanY = y.Average();

        double numerator = 0;
        double sumSquaredX = 0;
        double sumSquaredY = 0;

        // Calculate covariance and variances in single pass
        for (int i = 0; i < x.Count; i++)
        {
            double diffX = x[i] - meanX;
            double diffY = y[i] - meanY;

            numerator += diffX * diffY;        // Covariance term
            sumSquaredX += diffX * diffX;      // Variance of X
            sumSquaredY += diffY * diffY;      // Variance of Y
        }

        // Prevent division by zero for constant variables
        if (sumSquaredX == 0 || sumSquaredY == 0) return 0;

        return numerator / Math.Sqrt(sumSquaredX * sumSquaredY);
    }
}

/// <summary>
/// Container for comprehensive statistical metrics of a single column
/// Includes descriptive statistics, distribution metrics, and outlier detection results
/// </summary>
public class ColumnStatistics
{
    // Column identifier
    public string ColumnName { get; set; } = "";

    // === Basic Descriptive Statistics ===
    // Number of valid (non-null) values
    public int Count { get; set; }
    // Minimum value
    public double Min { get; set; }
    // Maximum value
    public double Max { get; set; }
    // Range (Max - Min)
    public double Range { get; set; }
    // Arithmetic mean (average)
    public double Mean { get; set; }
    // 50th percentile (median)
    public double Median { get; set; }
    // Sum of all values
    public double Sum { get; set; }

    // === Dispersion Measures ===
    // Standard deviation (measure of spread)
    public double StandardDeviation { get; set; }
    // Variance (squared standard deviation)
    public double Variance { get; set; }

    // === Percentiles (Distribution Quartiles) ===
    // 25th percentile (Q1 - first quartile)
    public double Percentile25 { get; set; }
    // 75th percentile (Q3 - third quartile)
    public double Percentile75 { get; set; }
    // 95th percentile
    public double Percentile95 { get; set; }

    // === Outlier Detection (Using IQR Method) ===
    // Interquartile Range (Q3 - Q1)
    public double IQR { get; set; }
    // Number of values outside outlier bounds
    public int OutlierCount { get; set; }
    // Lower outlier threshold (Q1 - 1.5 × IQR)
    public double LowerOutlierBound { get; set; }
    // Upper outlier threshold (Q3 + 1.5 × IQR)
    public double UpperOutlierBound { get; set; }

    // === Trend Analysis ===
    // Simple moving average of last N values
    public double MovingAverage { get; set; }
}

