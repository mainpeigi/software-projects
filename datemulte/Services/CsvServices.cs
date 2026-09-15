using CsvHelper;
using CsvHelper.Configuration;
using Datemulte_2.Models;
using System.Globalization;
using System.Text.RegularExpressions;
using OfficeOpenXml;
using Microsoft.Extensions.Logging;

namespace Datemulte_2.Services;

public partial class CsvService
{
    [GeneratedRegex(@"^[a-zA-Z]+(/[a-zA-Z]+)?[0-9]?$", RegexOptions.CultureInvariant)]
    private static partial Regex UnitPattern();

    private readonly ILogger<CsvService> _logger;

    public CsvService(ILogger<CsvService> logger)
    {
        _logger = logger;
    }

    public List<string> DetectedColumns { get; private set; } = new();
    public List<string> NumericColumns { get; private set; } = new();
    public string? TimeColumnName { get; private set; }
    public Dictionary<string, string> ColumnToGroupMapping { get; private set; } = new();
    public List<string> SensorGroups { get; private set; } = new();
    public Dictionary<string, string> ColumnUnits { get; private set; } = new();
    public bool IsGroupedFormat { get; private set; } = false;

    public async Task<(bool isExcel, List<string> sheetNames)> DetectExcelSheetsAsync(Stream stream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLower();
        if (extension != ".xlsx" && extension != ".xls")
        {
            return (false, new List<string>());
        }
        try
        {
            // Copy to MemoryStream to support synchronous reads required by EPPlus
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            using var package = new ExcelPackage(memoryStream);
            var sheetNames = package.Workbook.Worksheets.Select(ws => ws.Name).ToList();

            _logger.LogInformation("Detected Excel file with {SheetCount} sheets: {SheetNames}", sheetNames.Count, string.Join(", ", sheetNames));
            return (true, sheetNames);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting Excel sheets");
            return (false, new List<string>());
        }
    }

    public async Task<(List<GenericCsvRow> data, List<string> columns, List<string> numericColumns, string? timeColumn, Dictionary<string, string> columnGroups, List<string> sensorGroups, Dictionary<string, string> columnUnits)> ParseExcelSheetAsync(Stream stream, string sheetName, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var data = new List<GenericCsvRow>();
        var columns = new List<string>();
        var numericColumnsSet = new HashSet<string>();
        string? timeColumn = null;
        var columnGroups = new Dictionary<string, string>();
        var sensorGroupsSet = new HashSet<string>();
        var columnUnits = new Dictionary<string, string>();

        try
        {
            // Copy to MemoryStream to support synchronous reads required by EPPlus
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            using var package = new ExcelPackage(memoryStream);
            var worksheet = package.Workbook.Worksheets[sheetName];

            if (worksheet == null)
            {
                throw new Exception($"Sheet '{sheetName}' not found");
            }

            // Read first 3 rows to detect format
            var row1 = new List<string>();
            var row2 = new List<string>();
            var row3 = new List<string>();

            int colCount = worksheet.Dimension?.Columns ?? 0;

            for (int col = 1; col <= colCount; col++)
            {
                row1.Add(worksheet.Cells[1, col].Text?.Trim() ?? "");
                row2.Add(worksheet.Cells[2, col].Text?.Trim() ?? "");
                row3.Add(worksheet.Cells[3, col].Text?.Trim() ?? "");
            }

            // Detect grouped format
            bool isGroupedFormat = DetectGroupedFormat(row1.ToArray(), row2.ToArray(), row3.ToArray());
            IsGroupedFormat = isGroupedFormat;

            string[]? headers = null;
            int dataStartRow = 0;

            if (isGroupedFormat)
            {
                _logger.LogDebug("Detected GROUPED Excel format");
                var groupRow = row1.ToArray();
                headers = row2.ToArray();
                var unitRow = row3.ToArray();
                dataStartRow = 4;

                // Build column-to-group mapping and extract units
                string currentGroup = "";
                for (int i = 0; i < headers.Length && i < groupRow.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(groupRow[i]))
                    {
                        currentGroup = groupRow[i];
                        sensorGroupsSet.Add(currentGroup);
                    }

                    if (!string.IsNullOrWhiteSpace(headers[i]))
                    {
                        if (!string.IsNullOrEmpty(currentGroup))
                        {
                            columnGroups[headers[i]] = currentGroup;
                        }

                        // Extract unit from row 3
                        if (i < unitRow.Length && !string.IsNullOrWhiteSpace(unitRow[i]))
                        {
                            columnUnits[headers[i]] = unitRow[i];
                        }
                    }
                }

                columns = headers.Where(h => !string.IsNullOrWhiteSpace(h)).ToList();
            }
            else
            {
                _logger.LogDebug("Detected NORMAL Excel format");
                headers = row1.ToArray();
                columns = headers.Where(h => !string.IsNullOrWhiteSpace(h)).ToList();

                // Check if row 2 or row 3 contains units
                if (IsUnitsRow(row2.ToArray()))
                {
                    _logger.LogDebug("Row 2 detected as units row");
                    dataStartRow = 3;
                    for (int i = 0; i < headers.Length && i < row2.Count; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(headers[i]) && !string.IsNullOrWhiteSpace(row2[i]))
                        {
                            columnUnits[headers[i]] = row2[i];
                        }
                    }
                }
                else if (IsUnitsRow(row3.ToArray()))
                {
                    _logger.LogDebug("Row 3 detected as units row");
                    dataStartRow = 4;
                    for (int i = 0; i < headers.Length && i < row3.Count; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(headers[i]) && !string.IsNullOrWhiteSpace(row3[i]))
                        {
                            columnUnits[headers[i]] = row3[i];
                        }
                    }
                }
                else
                {
                    _logger.LogDebug("No units row detected, data starts from row 2");
                    dataStartRow = 2;
                }
            }

            // Identify time/date column
            foreach (var col in columns)
            {
                var lowerCol = col.ToLower();
                if (lowerCol.Contains("time") || lowerCol.Contains("date") || lowerCol.Contains("timestamp"))
                {
                    timeColumn = col;
                    break;
                }
            }

            // Read data rows
            int rowCount = worksheet.Dimension?.Rows ?? 0;

            // Build a mapping from column name → 0-based header index (for array lookup)
            var columnIndices = new Dictionary<string, int>();
            for (int i = 0; i < headers.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(headers[i]))
                    columnIndices[headers[i]] = i;
            }

            // Load all data cells into a single object[,] — one EPPlus call instead of one per cell
            object[,]? allValues = null;
            int dataRowCount = rowCount - dataStartRow + 1;
            if (dataRowCount > 0 && colCount > 0)
            {
                allValues = worksheet.Cells[dataStartRow, 1, rowCount, colCount].Value as object[,];
                data.Capacity = dataRowCount;
            }

            for (int row = dataStartRow; row <= rowCount; row++)
            {
                var rowData = new Dictionary<string, string>(columns.Count);
                bool hasData = false;

                // allValues is 1-based relative to the range start (row=dataStartRow → index 0)
                int arrayRow = row - dataStartRow;

                foreach (var col in columns)
                {
                    if (!columnIndices.TryGetValue(col, out int colIndex))
                        continue;

                    string cellValue;
                    if (allValues != null)
                    {
                        var raw = allValues[arrayRow, colIndex];
                        cellValue = raw switch
                        {
                            null => "",
                            double d => d.ToString(CultureInfo.InvariantCulture),
                            DateTime dt => dt.ToString("o"),
                            _ => raw.ToString()?.Trim() ?? ""
                        };
                    }
                    else
                    {
                        cellValue = worksheet.Cells[row, colIndex + 1].Text?.Trim() ?? "";
                    }

                    if (!string.IsNullOrWhiteSpace(cellValue))
                    {
                        hasData = true;
                        rowData[col] = cellValue;

                        if (col != timeColumn && !numericColumnsSet.Contains(col) &&
                            double.TryParse(cellValue, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                        {
                            numericColumnsSet.Add(col);
                        }
                    }
                    else
                    {
                        rowData[col] = "";
                    }
                }

                if (hasData)
                {
                    data.Add(new GenericCsvRow { Data = rowData });
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (progress != null && (row - dataStartRow) % 5000 == 0)
                    progress.Report(row - dataStartRow);
            }

            var numericColumns = numericColumnsSet.ToList();
            var sensorGroups = sensorGroupsSet.ToList();

            DetectedColumns = columns;
            NumericColumns = numericColumns;
            TimeColumnName = timeColumn;
            ColumnToGroupMapping = columnGroups;
            SensorGroups = sensorGroups;
            ColumnUnits = columnUnits;

            _logger.LogInformation("Parsed Excel sheet: {RowCount} rows, {ColumnCount} columns, {NumericCount} numeric columns",
                data.Count, columns.Count, numericColumns.Count);
            _logger.LogDebug("Time column: {TimeColumn}", timeColumn ?? "none");
            _logger.LogDebug("Columns: {Columns}", string.Join(", ", columns));
            _logger.LogDebug("Units: {Units}", string.Join(", ", columnUnits.Select(kvp => $"{kvp.Key}={kvp.Value}")));

            return await Task.FromResult((data, columns, numericColumns, timeColumn, columnGroups, sensorGroups, columnUnits));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Excel sheet");
            throw;
        }
    }

    public async Task<(List<GenericCsvRow> data, List<string> columns, List<string> numericColumns, string? timeColumn, Dictionary<string, string> columnGroups, List<string> sensorGroups, Dictionary<string, string> columnUnits)> ParseGenericCsvAsync(Stream stream, bool autoDetectDateColumns = true, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var data = new List<GenericCsvRow>();
        var columns = new List<string>();
        var numericColumns = new List<string>();
        string? timeColumn = null;
        var columnGroups = new Dictionary<string, string>();
        var sensorGroupsSet = new HashSet<string>();
        var columnUnits = new Dictionary<string, string>();

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,  // We'll handle headers manually to detect format
            TrimOptions = TrimOptions.Trim,
            BadDataFound = null,
            MissingFieldFound = null,
            IgnoreBlankLines = true,
            DetectDelimiter = true
        };

        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, config);

        // Read first 3 rows to detect format
        await csv.ReadAsync();
        var row1 = csv.Parser.Record?.Select(f => f?.Trim() ?? "").ToArray() ?? Array.Empty<string>();

        await csv.ReadAsync();
        var row2 = csv.Parser.Record?.Select(f => f?.Trim() ?? "").ToArray() ?? Array.Empty<string>();

        await csv.ReadAsync();
        var row3 = csv.Parser.Record?.Select(f => f?.Trim() ?? "").ToArray() ?? Array.Empty<string>();

        // Detect grouped format:
        // - Row 1 has group names (with empty cells or repeating values)
        // - Row 2 has column labels
        // - Row 3 has units or starts with data
        bool isGroupedFormat = DetectGroupedFormat(row1, row2, row3);
        IsGroupedFormat = isGroupedFormat;

        // Compute once here; reused in the else-branch and again when processing data rows
        bool row2IsUnits = !isGroupedFormat && IsUnitsRow(row2);

        string[]? headers = null;

        if (isGroupedFormat)
        {
            _logger.LogDebug("Detected GROUPED CSV format");
            // Row 1 = groups, Row 2 = headers, Row 3 = units, Row 4+ = data
            var groupRow = row1;
            headers = row2;
            var unitRow = row3;

            // Build column-to-group mapping and extract units
            string currentGroup = "";
            for (int i = 0; i < headers.Length && i < groupRow.Length; i++)
            {
                // Update current group if not empty
                if (!string.IsNullOrWhiteSpace(groupRow[i]))
                {
                    currentGroup = groupRow[i];
                    sensorGroupsSet.Add(currentGroup);
                }

                // Map column to group
                if (!string.IsNullOrWhiteSpace(headers[i]))
                {
                    columnGroups[headers[i]] = currentGroup;

                    // Extract unit for this column
                    if (i < unitRow.Length && !string.IsNullOrWhiteSpace(unitRow[i]) && unitRow[i] != "-")
                    {
                        columnUnits[headers[i]] = unitRow[i];
                        _logger.LogDebug("Column '{ColumnName}' has unit '{Unit}'", headers[i], unitRow[i]);
                    }
                }
            }

            _logger.LogDebug("Detected {GroupCount} sensor groups: {Groups}", sensorGroupsSet.Count, string.Join(", ", sensorGroupsSet));
            _logger.LogDebug("Detected {UnitCount} columns with units", columnUnits.Count);

            // Skip row 3 (units) and start reading data from row 4
            // Row 3 is already read, next Read will be row 4
        }
        else
        {
            _logger.LogDebug("Detected NORMAL CSV format");
            // Row 1 = headers, Row 2 could be units or data, Row 3+ = data
            headers = row1;

            if (row2IsUnits)
            {
                _logger.LogDebug("Row 2 detected as units row");
                // Row 2 = units, Row 3+ = data
                for (int i = 0; i < headers.Length && i < row2.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(row2[i]) && row2[i] != "-")
                    {
                        columnUnits[headers[i]] = row2[i];
                        _logger.LogDebug("Column '{ColumnName}' has unit '{Unit}'", headers[i], row2[i]);
                    }
                }
                // Start processing data from row3
                // Row3 will be processed as first data row below
            }
            else
            {
                _logger.LogDebug("Row 2 is data, not units");
                // Row 2 and 3 are data rows
                // They will be processed as data below
            }
        }

        if (headers != null)
        {
            columns = headers.ToList();

            // Detect time/date column by name
            timeColumn = columns.FirstOrDefault(c =>
                c.Contains("time", StringComparison.OrdinalIgnoreCase) ||
                c.Contains("date", StringComparison.OrdinalIgnoreCase) ||
                c.Contains("timestamp", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("ts", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("t", StringComparison.OrdinalIgnoreCase) && columns.Count > 1);

            // Read all rows and detect numeric columns at the same time
            var rowCount = 0;
            var columnSamples = new Dictionary<string, List<string>>();
            var columnHasData = new Dictionary<string, bool>();

            foreach (var col in columns)
            {
                columnSamples[col] = new List<string>();
                columnHasData[col] = false;
            }

            // For normal format, we need to process row2 and row3 as data rows (unless row2 is units)
            if (!isGroupedFormat)
            {
                if (!row2IsUnits)
                {
                    // Process row2 as first data row
                    ProcessDataRow(row2, headers, data, columnSamples, columnHasData, ref rowCount);
                }

                // Always process row3 as data row
                ProcessDataRow(row3, headers, data, columnSamples, columnHasData, ref rowCount);
            }

            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var currentRow = csv.Parser.Record?.Select(f => f ?? "").ToArray();
                if (currentRow != null)
                {
                    ProcessDataRow(currentRow, headers, data, columnSamples, columnHasData, ref rowCount);
                    if (progress != null && rowCount % 5000 == 0)
                        progress.Report(rowCount);
                }
            }

            // Add samples from the last 10 rows
            var lastRows = data.TakeLast(10);
            foreach (var lastRow in lastRows)
            {
                foreach (var header in headers)
                {
                    var value = lastRow.GetValue(header) ?? "";
                    if (!string.IsNullOrWhiteSpace(value) && columnSamples[header].Count < 100)
                    {
                        columnSamples[header].Add(value);
                    }
                }
            }

            // Auto-detect date columns if enabled
            if (autoDetectDateColumns && timeColumn == null)
            {
                foreach (var columnName in columns)
                {
                    var samples = columnSamples[columnName].Take(20).ToList();
                    if (samples.Count >= 5 && IsDateColumn(samples))
                    {
                        timeColumn = columnName;
                        _logger.LogDebug("Auto-detected date column: {ColumnName}", columnName);
                        break;
                    }
                }
            }

            // Determine which columns are numeric based on samples — each column is independent so run in parallel
            var detectedNumeric = new System.Collections.Concurrent.ConcurrentBag<string>();

            var columnsToCheck = columns.Where(c => c != timeColumn).ToList();
            Parallel.ForEach(columnsToCheck, columnName =>
            {
                var samples = columnSamples[columnName];

                // Filter out empty/whitespace values
                var validSamples = samples.Where(val => !string.IsNullOrWhiteSpace(val)).ToList();

                _logger.LogDebug("Column: '{ColumnName}' - Valid samples: {SampleCount}, First few: {Samples}",
                    columnName, validSamples.Count, string.Join(", ", validSamples.Take(5)));

                if (validSamples.Count == 0)
                {
                    // If column has data somewhere (even if not in samples), assume it might be numeric
                    if (columnHasData.ContainsKey(columnName) && columnHasData[columnName])
                    {
                        _logger.LogDebug("Column '{ColumnName}' has data but no samples captured, checking for any non-zero value", columnName);
                        var firstNonZero = data.Select(r => r.GetValue(columnName))
                            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v) && v != "0");

                        if (firstNonZero != null && TryParseNumericSpan(firstNonZero))
                        {
                            detectedNumeric.Add(columnName);
                            _logger.LogDebug("Column '{ColumnName}' ADDED based on data presence (value: {Value})", columnName, firstNonZero);
                        }
                        else
                        {
                            _logger.LogDebug("Column '{ColumnName}' skipped (no valid samples)", columnName);
                        }
                    }
                    return;
                }

                // Check if at least 20% of samples are numeric (lowered to handle sparse data)
                int numericCount = 0;
                foreach (var val in validSamples)
                {
                    if (TryParseNumericSpan(val))
                        numericCount++;
                }

                double numericPercentage = (double)numericCount / validSamples.Count;
                _logger.LogDebug("Column '{ColumnName}' - Numeric: {NumericCount}/{TotalCount} ({Percentage:P0})",
                    columnName, numericCount, validSamples.Count, numericPercentage);

                if (numericPercentage >= 0.2)
                {
                    detectedNumeric.Add(columnName);
                    _logger.LogDebug("Column '{ColumnName}' ADDED as numeric column", columnName);
                }
                else
                {
                    _logger.LogDebug("Column '{ColumnName}' NOT added (below 20% threshold)", columnName);
                }
            });

            // Preserve original column order
            numericColumns.AddRange(columns.Where(detectedNumeric.Contains));
        }

        var sensorGroups = sensorGroupsSet.ToList();

        DetectedColumns = columns;
        NumericColumns = numericColumns;
        TimeColumnName = timeColumn;
        ColumnToGroupMapping = columnGroups;
        SensorGroups = sensorGroups;
        ColumnUnits = columnUnits;

        return (data, columns, numericColumns, timeColumn, columnGroups, sensorGroups, columnUnits);
    }

    private static bool TryParseNumericSpan(string val)
    {
        var span = val.AsSpan().Trim();
        if (double.TryParse(span, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            return true;

        // Try comma-as-decimal-separator without allocating a new string
        if (span.Contains(','))
        {
            Span<char> buf = span.Length <= 64
                ? stackalloc char[span.Length]
                : new char[span.Length];
            span.CopyTo(buf);
            buf.Replace(',', '.');
            return double.TryParse(buf, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
        }

        return double.TryParse(span, NumberStyles.Any, CultureInfo.CurrentCulture, out _);
    }

    private bool IsUnitsRow(string[] row)
    {
        if (row == null || row.Length == 0) return false;

        // Count how many cells look like units (short strings with common unit patterns)
        int unitLikeCount = 0;
        int nonEmptyCount = 0;

        foreach (var cell in row)
        {
            if (string.IsNullOrWhiteSpace(cell)) continue;

            nonEmptyCount++;
            string trimmed = cell.Trim();

            // Check if it looks like a unit:
            // - Short length (typically <= 10 characters)
            // - Contains common unit patterns: /, °, ², ³, or just letters
            // - Is just a dash "-" (meaning no unit)
            // - Contains common units: m, kg, s, bar, Hz, etc.
            if (trimmed == "-" ||
                (trimmed.Length <= 10 && (
                    trimmed.Contains("/") ||
                    trimmed.Contains("°") ||
                    trimmed.Contains("²") ||
                    trimmed.Contains("³") ||
                    trimmed.Contains("m3") ||
                    trimmed.Contains("m2") ||
                    UnitPattern().IsMatch(trimmed)
                )))
            {
                unitLikeCount++;
            }
        }

        // If at least 30% of non-empty cells look like units, consider it a units row
        return nonEmptyCount > 0 && (double)unitLikeCount / nonEmptyCount >= 0.3;
    }

    public string FormatTimeToMinutesSeconds(string timeString, bool showHours = true, bool showMinutes = true, bool showSeconds = true, bool showMilliseconds = false)
    {
        if (string.IsNullOrEmpty(timeString)) return "00:00";

        try
        {
            // Try parsing as numeric value (seconds or milliseconds)
            if (double.TryParse(timeString, NumberStyles.Any, CultureInfo.InvariantCulture, out double numericValue))
            {
                // Determine if this is milliseconds or seconds
                // If value is very large (> 10000), it's likely milliseconds or a Unix timestamp
                double totalSeconds;

                if (numericValue > 10000)
                {
                    // Check if it's a Unix timestamp (seconds since 1970-01-01)
                    // Unix timestamps for recent dates are in the billions
                    if (numericValue > 1000000000)
                    {
                        // It's a Unix timestamp - convert to DateTime and format
                        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        var dateTime_spec = epoch.AddSeconds(numericValue);

                        // Build format string based on selected components
                        var unixTimeParts = new List<string>();
                        if (showHours) unixTimeParts.Add($"{dateTime_spec.Hour:D2}");
                        if (showMinutes) unixTimeParts.Add($"{dateTime_spec.Minute:D2}");
                        if (showSeconds) unixTimeParts.Add($"{dateTime_spec.Second:D2}");
                        if (showMilliseconds) unixTimeParts.Add($"{dateTime_spec.Millisecond:D3}");

                        return unixTimeParts.Count > 0 ? string.Join(":", unixTimeParts) : "00:00";
                    }
                    else
                    {
                        // Assume milliseconds - convert to seconds
                        totalSeconds = numericValue / 1000.0;
                    }
                }
                else
                {
                    // It's already in seconds
                    totalSeconds = numericValue;
                }

                // Calculate all components
                int hours = (int)(totalSeconds / 3600);
                int minutes = (int)((totalSeconds % 3600) / 60);
                int seconds = (int)(totalSeconds % 60);
                int milliseconds = (int)((totalSeconds % 1) * 1000);

                // Build format string based on selected components
                var timeParts = new List<string>();
                if (showHours) timeParts.Add($"{hours:D2}");
                if (showMinutes) timeParts.Add($"{minutes:D2}");
                if (showSeconds) timeParts.Add($"{seconds:D2}");
                if (showMilliseconds) timeParts.Add($"{milliseconds:D3}");

                return timeParts.Count > 0 ? string.Join(":", timeParts) : "00:00";
            }

            // Try parsing as DateTime (handles ISO 8601 and other formats)
            if (DateTime.TryParse(timeString, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTime))
            {
                return $"{dateTime.Minute:D2}:{dateTime.Second:D2}";
            }

            // Try parsing as TimeSpan (handles MM:SS or HH:MM:SS)
            if (TimeSpan.TryParse(timeString, out TimeSpan timeSpan))
            {
                int totalMinutes = (int)timeSpan.TotalMinutes;
                int seconds = timeSpan.Seconds;
                return $"{totalMinutes:D2}:{seconds:D2}";
            }

            // Fallback: try to extract MM:SS from string
            var parts = timeString.Split(':', ' ');
            if (parts.Length >= 2)
            {
                // Look for patterns like "HH:MM:SS" or "MM:SS"
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    if (int.TryParse(parts[i], out int val1) && int.TryParse(parts[i + 1], out int val2))
                    {
                        // If we have 3 parts, it's likely HH:MM:SS, take MM:SS
                        if (parts.Length >= 3 && i == 0)
                        {
                            return $"{val2:D2}:{int.Parse(parts[i + 2].Split('.', 'Z')[0]):D2}";
                        }
                        return $"{val1:D2}:{val2:D2}";
                    }
                }
            }

            return timeString;
        }
        catch
        {
            return timeString;
        }
    }

    private bool IsDateColumn(List<string> samples)
    {
        if (samples.Count < 5) return false;

        int dateCount = 0;
        var formats = new[]
        {
            "yyyy-MM-dd", "yyyy/MM/dd", "MM/dd/yyyy", "dd/MM/yyyy",
            "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.fff", "yyyy-MM-ddTHH:mm:ssZ"
        };

        foreach (var sample in samples)
        {
            if (string.IsNullOrWhiteSpace(sample)) continue;

            if (DateTime.TryParse(sample, CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
                formats.Any(fmt => DateTime.TryParseExact(sample, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
            {
                dateCount++;
            }
        }

        return (double)dateCount / samples.Count >= 0.8;
    }

    private void ProcessDataRow(string[] rowData, string[] headers, List<GenericCsvRow> data, Dictionary<string, List<string>> columnSamples, Dictionary<string, bool> columnHasData, ref int rowCount)
    {
        var row = new GenericCsvRow();

        for (int i = 0; i < headers.Length && i < rowData.Length; i++)
        {
            var header = headers[i];
            var value = rowData[i] ?? "";
            row.Data[header] = value;

            // Track if column has any non-empty data
            if (!string.IsNullOrWhiteSpace(value) && value != "0")
            {
                columnHasData[header] = true;
            }

            // Collect samples strategically
            if (rowCount < 50 || rowCount % 500 == 0)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    columnSamples[header].Add(value);
                }
            }
        }

        data.Add(row);
        rowCount++;
    }

    private bool DetectGroupedFormat(string[] row1, string[] row2, string[] row3)
    {
        if (row1.Length == 0 || row2.Length == 0 || row3.Length == 0)
            return false;

        // Check if row1 has sparse data (many empty cells) indicating it's a grouping row
        int emptyCount = row1.Count(s => string.IsNullOrWhiteSpace(s));
        double emptyRatio = (double)emptyCount / row1.Length;

        // Check if row2 looks like headers (contains "serial", "time", or other common header patterns)
        bool row2LooksLikeHeaders = row2.Any(s =>
            !string.IsNullOrWhiteSpace(s) && (
                s.Contains("serial", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("time", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("recorder", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("_", StringComparison.OrdinalIgnoreCase)
            ));

        // Check if row3 looks like units (short strings, symbols, dashes)
        bool row3LooksLikeUnits = row3.Count(s =>
            !string.IsNullOrWhiteSpace(s) && s.Length <= 10 &&
            (s.Contains("/") || s == "-" || s.All(c => char.IsLetter(c) || c == '/' || c == '3' || c == '2'))
        ) >= row3.Length * 0.3;

        // If row1 has >30% empty cells AND row2 looks like headers AND row3 looks like units
        bool isGrouped = emptyRatio > 0.3 && row2LooksLikeHeaders && row3LooksLikeUnits;

        _logger.LogDebug("Format detection: emptyRatio={EmptyRatio:P0}, row2Headers={Row2Headers}, row3Units={Row3Units}, isGrouped={IsGrouped}", 
            emptyRatio, row2LooksLikeHeaders, row3LooksLikeUnits, isGrouped);

        return isGrouped;
    }

    public ZeroValueDetectionResult DetectZeroValues(List<GenericCsvRow> data, List<string> numericColumns)
    {
        var result = new ZeroValueDetectionResult
        {
            TotalRows = data.Count
        };

        // Performance optimization: Sample first 1000 rows or use all if dataset is small
        int sampleSize = Math.Min(1000, data.Count);
        var sampleData = data.Take(sampleSize).ToList();

        _logger.LogDebug("DetectZeroValues: Checking {SampleSize} rows across {ColumnCount} columns", sampleSize, numericColumns.Count);

        foreach (var column in numericColumns)
        {
            int zeroCount = 0;
            int checkedCount = 0;

            foreach (var row in sampleData)
            {
                var valueStr = row.GetValue(column);

                // Skip empty/null values
                if (string.IsNullOrWhiteSpace(valueStr))
                    continue;

                checkedCount++;

                // Quick check: if the string is "0" or "0.0" etc, it's a zero
                if (valueStr.Trim() == "0" || valueStr.Trim() == "0.0" || valueStr.Trim() == "0,0")
                {
                    zeroCount++;
                    continue;
                }

                // Otherwise, parse it
                var value = row.GetNumericValueOrNull(column);
                if (value is double d && d == 0.0)
                {
                    zeroCount++;
                }
            }

            if (zeroCount > 0)
            {
                // Extrapolate to full dataset if we sampled
                int estimatedTotal = sampleSize < data.Count
                    ? (int)((double)zeroCount / sampleSize * data.Count)
                    : zeroCount;

                result.ZeroCountByColumn[column] = estimatedTotal;
                result.TotalZeros += estimatedTotal;

                _logger.LogDebug("Column '{Column}': {ZeroCount} zeros in sample ({CheckedCount} checked) -> estimated {EstimatedTotal} total", 
                    column, zeroCount, checkedCount, estimatedTotal);
            }
        }

        result.HasZeros = result.ZeroCountByColumn.Count > 0;
        _logger.LogDebug("DetectZeroValues result: HasZeros={HasZeros}, TotalZeros={TotalZeros}, Columns with zeros={ColumnCount}", 
            result.HasZeros, result.TotalZeros, result.ZeroCountByColumn.Count);

        return result;
    }

    public List<GenericCsvRow> FilterZeroValues(List<GenericCsvRow> data, List<string> columnsToFilter)
    {
        return data.Where(row =>
        {
            foreach (var column in columnsToFilter)
            {
                var value = row.GetNumericValueOrNull(column);
                if (value is double d && d == 0.0)
                {
                    return false;
                }
            }
            return true;
        }).ToList();
    }
}
