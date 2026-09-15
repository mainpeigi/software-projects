namespace Datemulte_2.Models;

/// <summary>
/// Generic CSV row representation using dynamic column-value dictionary
/// Allows parsing of any CSV file structure without predefined schema
/// Provides safe numeric conversion methods with multiple culture fallbacks
/// </summary>
public class GenericCsvRow
{
    // Dictionary storing column names as keys and cell values as strings
    public Dictionary<string, string> Data { get; set; } = new();

    /// <summary>
    /// Gets the raw string value for a column
    /// </summary>
    /// <param name="columnName">Column name to retrieve</param>
    /// <returns>String value or null if column doesn't exist</returns>
    public string? GetValue(string columnName)
    {
        return Data.TryGetValue(columnName, out var value) ? value : null;
    }

    /// <summary>
    /// Gets the numeric value for a column, returning 0.0 if parsing fails
    /// </summary>
    /// <param name="columnName">Column name to retrieve</param>
    /// <returns>Parsed double value or 0.0 if invalid/missing</returns>
    public double GetNumericValue(string columnName)
    {
        return TryParseNumeric(columnName, out double result) ? result : 0.0;
    }

    /// <summary>
    /// Gets the numeric value for a column as nullable object
    /// Useful for distinguishing between actual zero values and unparseable data
    /// </summary>
    /// <param name="columnName">Column name to retrieve</param>
    /// <returns>Boxed double value or null if parsing fails</returns>
    public object? GetNumericValueOrNull(string columnName)
    {
        return TryParseNumeric(columnName, out double result) ? (object)result : null;
    }

    /// <summary>
    /// Attempts to parse a column value as numeric with multiple culture fallbacks
    /// Tries: InvariantCulture, CurrentCulture, then comma-to-dot normalization
    /// </summary>
    /// <param name="columnName">Column name to parse</param>
    /// <param name="result">Parsed double value if successful</param>
    /// <returns>True if parsing succeeded, false otherwise</returns>
    private bool TryParseNumeric(string columnName, out double result)
    {
        result = 0.0;

        // Check if column exists
        if(!Data.TryGetValue(columnName, out var value))
            return false;

        value = value?.Trim() ?? "";

        // Empty values are not valid numbers
        if(string.IsNullOrEmpty(value))
            return false;

        // Try parsing with invariant culture (standard decimal point)
        if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out result))
            return true;

        // Try parsing with current culture (respects regional settings)
        if(double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out result))
            return true;

        // Fallback: normalize comma to dot and try again
        var normalizedValue = value.Replace(",", ".");

        if(double.TryParse(normalizedValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out result))
            return true;

        return false;
    }
}
