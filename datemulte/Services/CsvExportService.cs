using System.Text;
using Datemulte_2.Models;

namespace Datemulte_2.Services;

/// <summary>
/// Service for exporting data to CSV format with proper escaping and delimiter handling
/// Supports both regular data export and combined regular + calculated column export
/// Handles CSV special characters (delimiter, quotes, newlines) according to RFC 4180
/// </summary>
public class CsvExportService
{
    /// <summary>
    /// Exports data to CSV format as UTF-8 encoded byte array
    /// Properly escapes values containing delimiters, quotes, or newlines
    /// </summary>
    /// <param name="data">Data rows to export</param>
    /// <param name="columns">Columns to include in export</param>
    /// <param name="delimiter">Column delimiter (default: comma)</param>
    /// <param name="includeHeader">Include header row with column names</param>
    /// <returns>UTF-8 encoded CSV file as byte array</returns>
    public byte[] ExportToCsv(
        List<GenericCsvRow> data,
        List<string> columns,
        string delimiter = ",",
        bool includeHeader = true)
    {
        var sb = new StringBuilder();

        // Add header row if requested
        if (includeHeader)
        {
            sb.AppendLine(string.Join(delimiter, columns));
        }

        // Add data rows
        foreach (var row in data)
        {
            var values = columns.Select(col =>
            {
                var value = row.GetValue(col) ?? "";
                // Escape values containing delimiter, quotes, or newlines (RFC 4180)
                if (value.Contains(delimiter) || value.Contains("\"") || value.Contains("\n"))
                {
                    value = "\"" + value.Replace("\"", "\"\"") + "\"";  // Double-quote escaping
                }
                return value;
            });

            sb.AppendLine(string.Join(delimiter, values));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>
    /// Exports data with both regular and calculated columns to CSV format
    /// Calculated columns are appended after regular columns
    /// Regular columns use CSV escaping, calculated values use fixed-point formatting (6 decimals)
    /// </summary>
    /// <param name="data">Data rows to export</param>
    /// <param name="regularColumns">Regular data columns</param>
    /// <param name="calculatedData">Calculated columns with their computed values</param>
    /// <param name="delimiter">Column delimiter (default: comma)</param>
    /// <param name="includeHeader">Include header row with column names</param>
    /// <returns>UTF-8 encoded CSV file as byte array</returns>
    public byte[] ExportCalculatedDataToCsv(
        List<GenericCsvRow> data,
        List<string> regularColumns,
        Dictionary<string, List<double>> calculatedData,
        string delimiter = ",",
        bool includeHeader = true)
    {
        var sb = new StringBuilder();
        var allColumns = regularColumns.Concat(calculatedData.Keys).ToList();

        // Add header row if requested
        if (includeHeader)
        {
            sb.AppendLine(string.Join(delimiter, allColumns));
        }

        // Add data rows
        for (int i = 0; i < data.Count; i++)
        {
            var values = new List<string>();

            // Add regular column values with CSV escaping
            foreach (var col in regularColumns)
            {
                var value = data[i].GetValue(col) ?? "";
                if (value.Contains(delimiter) || value.Contains("\"") || value.Contains("\n"))
                {
                    value = "\"" + value.Replace("\"", "\"\"") + "\"";
                }
                values.Add(value);
            }

            // Add calculated column values (formatted as 6 decimal places)
            foreach (var calc in calculatedData)
            {
                if (i < calc.Value.Count)
                {
                    values.Add(calc.Value[i].ToString("F6"));  // Fixed-point 6 decimals
                }
                else
                {
                    values.Add("");  // Handle missing data gracefully
                }
            }

            sb.AppendLine(string.Join(delimiter, values));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
