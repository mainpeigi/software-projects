using Datemulte_2.Models;
using System.Text.RegularExpressions;

namespace Datemulte_2.Services;

public class ColumnReference
{
    public string ColumnName { get; set; } = "";
    public int? StartRow { get; set; }
    public int? EndRow { get; set; }
}

public class FormulaParser
{
    // Parse a formula like "=SUM(Column1{10:100})" or "=Column1{5:50} + Column2 * 0.5"
    public static (bool success, string errorMessage, Func<List<GenericCsvRow>, List<double>>? calculator) ParseFormula(
        string formula,
        List<string> availableColumns)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return (false, "Formula is empty", null);
        }

        if (!formula.StartsWith("="))
        {
            return (false, "Formula must start with '='", null);
        }

        var expression = formula.Substring(1).Trim();

        // Try to parse as Excel function first (SUM, AVERAGE, etc.)
        var functionResult = TryParseExcelFunction(expression, availableColumns);
        if (functionResult.success)
        {
            return functionResult;
        }

        // Try to parse as arithmetic expression (Column1 + Column2, etc.)
        var arithmeticResult = TryParseArithmetic(expression, availableColumns);
        if (arithmeticResult.success)
        {
            return arithmeticResult;
        }

        return (false, "Invalid formula syntax", null);
    }

    private static (bool success, string errorMessage, Func<List<GenericCsvRow>, List<double>>? calculator) TryParseExcelFunction(
        string expression,
        List<string> availableColumns)
    {
        // Match sensor-focused Excel functions
        var match = Regex.Match(expression, @"^(SUM|AVERAGE|AVG|MEDIAN|MAX|MIN|COUNT|COUNTA|IF|IFS|ROUND|ABS|STDEV|VAR|CORREL|AVERAGEIF|SUMIF|NOW|HOUR|MINUTE|SECOND|TEXT)\s*\(\s*(.+)\s*\)$", RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            return (false, "Not an Excel function", null);
        }

        var function = match.Groups[1].Value.ToUpper();
        var argumentsString = match.Groups[2].Value.Trim();

        // Handle different argument patterns based on function
        List<string> columnNames = new List<string>();
        string? conditionColumn = null;
        string? trueValue = null;
        string? falseValue = null;
        int? numDigits = null;
        string? criteria = null;
        string? formatPattern = null;
        List<(string condition, string result)>? ifsConditions = null;

        // Parse arguments based on function type
        if (function == "IF")
        {
            // IF(condition_column, value_if_true, value_if_false)
            var ifArgs = SplitArguments(argumentsString);
            if (ifArgs.Count != 3)
            {
                return (false, "IF function requires 3 arguments: IF(column, value_if_true, value_if_false)", null);
            }
            conditionColumn = ifArgs[0].Trim();
            var (colName, _, _) = ParseColumnReference(conditionColumn);

            trueValue = ifArgs[1].Trim().Trim('"', '\'');
            falseValue = ifArgs[2].Trim().Trim('"', '\'');

            if (!availableColumns.Contains(colName))
            {
                return (false, $"Column '{colName}' not found", null);
            }
        }
        else if (function == "IFS")
        {
            // IFS(condition1, result1, condition2, result2, ...)
            var ifsArgs = SplitArguments(argumentsString);
            if (ifsArgs.Count < 2 || ifsArgs.Count % 2 != 0)
            {
                return (false, "IFS function requires pairs of (condition, result) arguments", null);
            }

            ifsConditions = new List<(string, string)>();
            for (int i = 0; i < ifsArgs.Count; i += 2)
            {
                var condition = ifsArgs[i].Trim();
                var result = ifsArgs[i + 1].Trim().Trim('"', '\'');
                ifsConditions.Add((condition, result));
            }
        }
        else if (function == "ROUND")
        {
            // ROUND(column, num_digits)
            var args = SplitArguments(argumentsString);
            if (args.Count != 2)
            {
                return (false, "ROUND function requires 2 arguments: ROUND(column, num_digits)", null);
            }
            var colRef = args[0].Trim();
            var (colName, _, _) = ParseColumnReference(colRef);
            if (!availableColumns.Contains(colName))
            {
                return (false, $"Column '{colName}' not found", null);
            }
            columnNames.Add(colRef);

            if (!int.TryParse(args[1].Trim(), out var digits))
            {
                return (false, "Second argument must be a number", null);
            }
            numDigits = digits;
        }
        else if (function == "ABS" || function == "STDEV" || function == "VAR")
        {
            // Single column functions
            var colRef = argumentsString.Trim();
            var (colName, _, _) = ParseColumnReference(colRef);
            if (!availableColumns.Contains(colName))
            {
                return (false, $"Column '{colName}' not found", null);
            }
            columnNames.Add(colRef);
        }
        else if (function == "MEDIAN")
        {
            // MEDIAN can take multiple columns
            var colRefs = argumentsString.Split(',')
                .Select(c => c.Trim())
                .ToList();

            foreach (var colRef in colRefs)
            {
                var (colName, _, _) = ParseColumnReference(colRef);
                if (!availableColumns.Contains(colName))
                {
                    return (false, $"Column '{colName}' not found", null);
                }
                columnNames.Add(colRef);
            }
        }
        else if (function == "AVERAGEIF" || function == "SUMIF")
        {
            // AVERAGEIF/SUMIF(range, criteria, [average_range])
            var args = SplitArguments(argumentsString);
            if (args.Count < 2 || args.Count > 3)
            {
                return (false, $"{function} function requires 2 or 3 arguments: {function}(range, criteria, [sum/avg_range])", null);
            }

            var rangeRef = args[0].Trim();
            var (rangeCol, _, _) = ParseColumnReference(rangeRef);
            if (!availableColumns.Contains(rangeCol))
            {
                return (false, $"Column '{rangeCol}' not found", null);
            }
            conditionColumn = rangeRef;
            criteria = args[1].Trim().Trim('"', '\'');

            if (args.Count == 3)
            {
                var sumColRef = args[2].Trim();
                var (sumCol, _, _) = ParseColumnReference(sumColRef);
                if (!availableColumns.Contains(sumCol))
                {
                    return (false, $"Column '{sumCol}' not found", null);
                }
                columnNames.Add(sumColRef);
            }
            else
            {
                columnNames.Add(rangeRef); // Use same column
            }
        }
        else if (function == "CORREL")
        {
            // CORREL(array1, array2)
            var args = SplitArguments(argumentsString);
            if (args.Count != 2)
            {
                return (false, "CORREL function requires 2 arguments: CORREL(column1, column2)", null);
            }

            foreach (var arg in args)
            {
                var colRef = arg.Trim();
                var (colName, _, _) = ParseColumnReference(colRef);
                if (!availableColumns.Contains(colName))
                {
                    return (false, $"Column '{colName}' not found", null);
                }
                columnNames.Add(colRef);
            }
        }
        else if (function == "HOUR" || function == "MINUTE" || function == "SECOND")
        {
            // Time extraction functions
            var colRef = argumentsString.Trim();
            var (colName, _, _) = ParseColumnReference(colRef);
            if (!availableColumns.Contains(colName))
            {
                return (false, $"Column '{colName}' not found", null);
            }
            columnNames.Add(colRef);
        }
        else if (function == "TEXT")
        {
            // TEXT(column, "format_pattern")
            var args = SplitArguments(argumentsString);
            if (args.Count != 2)
            {
                return (false, "TEXT function requires 2 arguments: TEXT(column, format_pattern)", null);
            }
            var colRef = args[0].Trim();
            var (colName, _, _) = ParseColumnReference(colRef);
            if (!availableColumns.Contains(colName))
            {
                return (false, $"Column '{colName}' not found", null);
            }
            columnNames.Add(colRef);
            formatPattern = args[1].Trim().Trim('"', '\'');
        }
        else if (function == "NOW")
        {
            // NOW() - no arguments needed
        }
        else
        {
            // Standard multi-column functions: SUM, AVERAGE, MIN, MAX, COUNT, COUNTA
            var colRefs = argumentsString.Split(',')
                .Select(c => c.Trim())
                .ToList();

            foreach (var colRef in colRefs)
            {
                var (colName, _, _) = ParseColumnReference(colRef);
                if (!availableColumns.Contains(colName))
                {
                    return (false, $"Column '{colName}' not found", null);
                }
                columnNames.Add(colRef); // Store the full reference including range
            }
        }

        Func<List<GenericCsvRow>, List<double>> calculator = function switch
        {
            "SUM" => (data) =>
            {
                var results = new List<double>();
                for (int i = 0; i < data.Count; i++)
                {
                    var row = data[i];
                    var sum = columnNames.Sum(colRef => GetNumericValueWithRange(row, colRef, i));
                    results.Add(sum);
                }
                return results;
            },
            "AVERAGE" or "AVG" => (data) =>
            {
                var results = new List<double>();
                
                if (columnNames.Count == 1)
                {
                    var colRef = columnNames[0];
                    var (colName, startRow, endRow) = ParseColumnReference(colRef);
                    
                    if (startRow.HasValue && endRow.HasValue)
                    {
                        var windowSize = endRow.Value - startRow.Value + 1;
                        
                        for (int i = 0; i < data.Count; i++)
                        {
                            var windowStart = startRow.Value + i;
                            var windowEnd = endRow.Value + i;
                            
                            if (i >= data.Count - windowSize)
                            {
                                results.Add(0);
                            }
                            else
                            {
                                var windowValues = new List<double>();
                                for (int j = windowStart; j <= windowEnd && j < data.Count; j++)
                                {
                                    windowValues.Add(data[j].GetNumericValue(colName));
                                }
                                var avg = windowValues.Any() ? windowValues.Average() : 0;
                                results.Add(avg);
                            }
                        }
                        return results;
                    }
                }
                
                for (int i = 0; i < data.Count; i++)
                {
                    var row = data[i];
                    var values = columnNames.Select(colRef => GetNumericValueWithRange(row, colRef, i)).ToList();
                    var avg = values.Any() ? values.Average() : 0;
                    results.Add(avg);
                }
                return results;
            },
            "MEDIAN" => (data) =>
            {
                var results = new List<double>();
                for (int i = 0; i < data.Count; i++)
                {
                    var row = data[i];
                    var values = columnNames.Select(colRef => GetNumericValueWithRange(row, colRef, i))
                                           .OrderBy(v => v)
                                           .ToList();

                    if (values.Count == 0)
                    {
                        results.Add(0);
                    }
                    else if (values.Count % 2 == 1)
                    {
                        // Odd number of values - take middle one
                        results.Add(values[values.Count / 2]);
                    }
                    else
                    {
                        // Even number of values - average the two middle ones
                        var mid = values.Count / 2;
                        results.Add((values[mid - 1] + values[mid]) / 2.0);
                    }
                }
                return results;
            },
            "MIN" => (data) =>
            {
                var results = new List<double>();
                for (int i = 0; i < data.Count; i++)
                {
                    var row = data[i];
                    var values = columnNames.Select(colRef => GetNumericValueWithRange(row, colRef, i)).ToList();
                    var min = values.Any() ? values.Min() : 0;
                    results.Add(min);
                }
                return results;
            },
            "MAX" => (data) =>
            {
                var results = new List<double>();
                for (int i = 0; i < data.Count; i++)
                {
                    var row = data[i];
                    var values = columnNames.Select(colRef => GetNumericValueWithRange(row, colRef, i)).ToList();
                    var max = values.Any() ? values.Max() : 0;
                    results.Add(max);
                }
                return results;
            },
            "COUNT" => (data) =>
            {
                var results = new List<double>();
                for (int i = 0; i < data.Count; i++)
                {
                    var row = data[i];
                    var count = (double)columnNames.Count(colRef =>
                    {
                        var val = GetValueWithRange(row, colRef, i);
                        return val != null && !string.IsNullOrWhiteSpace(val) && double.TryParse(val, out _);
                    });
                    results.Add(count);
                }
                return results;
            },
            "COUNTA" => (data) =>
            {
                var results = new List<double>();
                for (int i = 0; i < data.Count; i++)
                {
                    var row = data[i];
                    var count = (double)columnNames.Count(colRef =>
                    {
                        var val = GetValueWithRange(row, colRef, i);
                        return val != null && !string.IsNullOrWhiteSpace(val);
                    });
                    results.Add(count);
                }
                return results;
            },
            "IF" => (data) =>
            {
                var results = new List<double>();
                for (int i = 0; i < data.Count; i++)
                {
                    var row = data[i];
                    var conditionValue = GetValueWithRange(row, conditionColumn!, i);

                    // Check if condition is "truthy"
                    bool isTrue = false;
                    if (double.TryParse(conditionValue, out var numValue))
                    {
                        isTrue = numValue != 0;
                    }
                    else
                    {
                        isTrue = !string.IsNullOrWhiteSpace(conditionValue) &&
                                 !conditionValue.Equals("false", StringComparison.OrdinalIgnoreCase);
                    }

                    var resultValue = isTrue ? trueValue! : falseValue!;

                    // Try to parse result as number, otherwise return length
                    if (double.TryParse(resultValue, out var numResult))
                    {
                        results.Add(numResult);
                    }
                    else
                    {
                        results.Add(resultValue.Length);
                    }
                }
                return results;
            },
            "IFS" => (data) =>
            {
                var results = new List<double>();
                for (int i = 0; i < data.Count; i++)
                {
                    var row = data[i];
                    string? matchedResult = null;

                    foreach (var (condition, result) in ifsConditions!)
                    {
                        // Parse condition like "B2<1" or "B2>=5"
                        var condMatch = Regex.Match(condition, @"([<>=]+)\s*(.+)");
                        if (condMatch.Success)
                        {
                            var op = condMatch.Groups[1].Value;
                            var threshold = condMatch.Groups[2].Value.Trim();

                            // Extract column reference (everything before operator)
                            var colRef = condition.Substring(0, condition.IndexOf(op)).Trim();
                            var value = GetValueWithRange(row, colRef, i);

                            if (EvaluateCriteria(value, op + threshold))
                            {
                                matchedResult = result;
                                break;
                            }
                        }
                    }

                    if (matchedResult != null)
                    {
                        if (double.TryParse(matchedResult, out var numResult))
                        {
                            results.Add(numResult);
                        }
                        else
                        {
                            results.Add(matchedResult.Length);
                        }
                    }
                    else
                    {
                        results.Add(0); // No condition matched
                    }
                }
                return results;
            },
            "ROUND" => (data) =>
            {
                var results = new List<double>();
                var colRef = columnNames[0];
                for (int i = 0; i < data.Count; i++)
                {
                    var row = data[i];
                    var value = GetNumericValueWithRange(row, colRef, i);
                    var factor = Math.Pow(10, numDigits!.Value);
                    results.Add(Math.Round(value * factor) / factor);
                }
                return results;
            },
            "ABS" => (data) =>
            {
                var results = new List<double>();
                var colRef = columnNames[0];
                for (int i = 0; i < data.Count; i++)
                {
                    var row = data[i];
                    var value = GetNumericValueWithRange(row, colRef, i);
                    results.Add(Math.Abs(value));
                }
                return results;
            },
            "STDEV" => (data) =>
            {
                var results = new List<double>();
                var colRef = columnNames[0];

                // Calculate overall stdev for the column, then repeat it
                var allValues = data.Select((row, i) => GetNumericValueWithRange(row, colRef, i)).ToList();
                if (allValues.Count > 1)
                {
                    var mean = allValues.Average();
                    var sumOfSquares = allValues.Sum(v => Math.Pow(v - mean, 2));
                    var stdDev = Math.Sqrt(sumOfSquares / (allValues.Count - 1));
                    results = Enumerable.Repeat(stdDev, data.Count).ToList();
                }
                else
                {
                    results = Enumerable.Repeat(0.0, data.Count).ToList();
                }
                return results;
            },
            "VAR" => (data) =>
            {
                var results = new List<double>();
                var colRef = columnNames[0];

                // Calculate overall variance for the column
                var allValues = data.Select((row, i) => GetNumericValueWithRange(row, colRef, i)).ToList();
                if (allValues.Count > 1)
                {
                    var mean = allValues.Average();
                    var variance = allValues.Sum(v => Math.Pow(v - mean, 2)) / (allValues.Count - 1);
                    results = Enumerable.Repeat(variance, data.Count).ToList();
                }
                else
                {
                    results = Enumerable.Repeat(0.0, data.Count).ToList();
                }
                return results;
            },
            "CORREL" => (data) =>
            {
                var colRef1 = columnNames[0];
                var colRef2 = columnNames[1];

                var values1 = data.Select((row, i) => GetNumericValueWithRange(row, colRef1, i)).ToList();
                var values2 = data.Select((row, i) => GetNumericValueWithRange(row, colRef2, i)).ToList();

                if (values1.Count > 1)
                {
                    var mean1 = values1.Average();
                    var mean2 = values2.Average();
                    var covariance = values1.Zip(values2, (x, y) => (x - mean1) * (y - mean2)).Sum();
                    var stdDev1 = Math.Sqrt(values1.Sum(v => Math.Pow(v - mean1, 2)));
                    var stdDev2 = Math.Sqrt(values2.Sum(v => Math.Pow(v - mean2, 2)));

                    var correlation = stdDev1 != 0 && stdDev2 != 0 ? covariance / (stdDev1 * stdDev2) : 0;
                    return Enumerable.Repeat(correlation, data.Count).ToList();
                }
                return Enumerable.Repeat(0.0, data.Count).ToList();
            },
            "AVERAGEIF" => (data) =>
            {
                var results = new List<double>();
                var sumColRef = columnNames[0];

                for (int i = 0; i < data.Count; i++)
                {
                    var row = data[i];
                    var condValue = GetValueWithRange(row, conditionColumn!, i);
                    bool matchesCriteria = EvaluateCriteria(condValue, criteria!);

                    if (matchesCriteria)
                    {
                        results.Add(GetNumericValueWithRange(row, sumColRef, i));
                    }
                    else
                    {
                        results.Add(0);
                    }
                }
                return results;
            },
            "SUMIF" => (data) =>
            {
                var results = new List<double>();
                var sumColRef = columnNames[0];

                for (int i = 0; i < data.Count; i++)
                {
                    var row = data[i];
                    var condValue = GetValueWithRange(row, conditionColumn!, i);
                    bool matchesCriteria = EvaluateCriteria(condValue, criteria!);

                    if (matchesCriteria)
                    {
                        results.Add(GetNumericValueWithRange(row, sumColRef, i));
                    }
                    else
                    {
                        results.Add(0);
                    }
                }
                return results;
            },
            "NOW" => (data) =>
            {
                var timestamp = DateTime.Now.ToOADate(); // Excel date format
                return Enumerable.Repeat(timestamp, data.Count).ToList();
            },
            "HOUR" => (data) =>
            {
                var results = new List<double>();
                var colRef = columnNames[0];

                for (int i = 0; i < data.Count; i++)
                {
                    var row = data[i];
                    var value = GetValueWithRange(row, colRef, i);

                    if (DateTime.TryParse(value, out var dt))
                    {
                        results.Add(dt.Hour);
                    }
                    else
                    {
                        results.Add(0);
                    }
                }
                return results;
            },
            "MINUTE" => (data) =>
            {
                var results = new List<double>();
                var colRef = columnNames[0];

                for (int i = 0; i < data.Count; i++)
                {
                    var row = data[i];
                    var value = GetValueWithRange(row, colRef, i);

                    if (DateTime.TryParse(value, out var dt))
                    {
                        results.Add(dt.Minute);
                    }
                    else
                    {
                        results.Add(0);
                    }
                }
                return results;
            },
            "SECOND" => (data) =>
            {
                var results = new List<double>();
                var colRef = columnNames[0];

                for (int i = 0; i < data.Count; i++)
                {
                    var row = data[i];
                    var value = GetValueWithRange(row, colRef, i);

                    if (DateTime.TryParse(value, out var dt))
                    {
                        results.Add(dt.Second);
                    }
                    else
                    {
                        results.Add(0);
                    }
                }
                return results;
            },
            "TEXT" => (data) =>
            {
                var results = new List<double>();
                var colRef = columnNames[0];

                for (int i = 0; i < data.Count; i++)
                {
                    var row = data[i];
                    var value = GetValueWithRange(row, colRef, i);

                    if (DateTime.TryParse(value, out var dt))
                    {
                        var formatted = dt.ToString(formatPattern!.Replace("hh", "HH")); // Convert Excel format
                        results.Add(formatted.GetHashCode()); // Return hash as numeric
                    }
                    else
                    {
                        results.Add(0);
                    }
                }
                return results;
            },
            _ => throw new NotImplementedException($"Function {function} not implemented")
        };

        return (true, "", calculator);
    }

    private static (bool success, string errorMessage, Func<List<GenericCsvRow>, List<double>>? calculator) TryParseArithmetic(
        string expression,
        List<string> availableColumns)
    {
        // Simple arithmetic parser for expressions like: Column1{10:50} + Column2, Column1 * 0.5, etc.
        try
        {
            Console.WriteLine($"=== Parsing arithmetic expression: '{expression}'");
            Console.WriteLine($"=== Available columns ({availableColumns.Count}): {string.Join(", ", availableColumns.Take(20))}");

            var testExpression = expression;
            var columnReferences = new List<string>();
            var columnMappings = new Dictionary<string, string>(); // Maps found reference to actual column name

            // Strategy: Try to match available column names directly in the expression
            // This handles column names with special characters like brackets, dots, etc.

            // Sort columns by length (longest first) to match "Column[A]" before "Column"
            foreach (var colName in availableColumns.OrderByDescending(c => c.Length))
            {
                // Skip empty column names
                if (string.IsNullOrWhiteSpace(colName))
                    continue;

                // Check if this exact column name appears in the expression (case-insensitive)
                // Look for the column name followed by optional range notation {start:end}
                var colIndex = expression.IndexOf(colName, StringComparison.OrdinalIgnoreCase);

                if (colIndex >= 0)
                {
                    // Check if there's a range notation after the column name
                    var rangeMatch = Regex.Match(expression.Substring(colIndex),
                        $@"^{Regex.Escape(colName)}(\{{\d+:\d+\}})?",
                        RegexOptions.IgnoreCase);

                    if (rangeMatch.Success)
                    {
                        var colRef = rangeMatch.Value;
                        if (!string.IsNullOrWhiteSpace(colRef) && !columnReferences.Contains(colRef, StringComparer.OrdinalIgnoreCase))
                        {
                            columnReferences.Add(colRef);
                            columnMappings[colRef] = colName; // Store actual column name
                        }
                    }
                }
            }

            if (columnReferences.Count == 0)
            {
                // Provide helpful error message with available columns
                var availableColumnsStr = availableColumns.Count > 0
                    ? string.Join(", ", availableColumns.Take(5)) + (availableColumns.Count > 5 ? $", ... ({availableColumns.Count} total)" : "")
                    : "none";
                return (false, $"No column names found in your formula. Available numeric columns: {availableColumnsStr}", null);
            }

            Console.WriteLine($"Found {columnReferences.Count} column references:");
            for (int i = 0; i < columnReferences.Count; i++)
            {
                Console.WriteLine($"  [{i}] = '{columnReferences[i]}' (length: {columnReferences[i].Length})");
            }

            // Build test expression - replace column references with 0 for syntax validation
            testExpression = expression;
            foreach (var colRef in columnReferences.OrderByDescending(c => c.Length))
            {
                var escapedPattern = Regex.Escape(colRef);
                testExpression = Regex.Replace(testExpression, escapedPattern, "0", RegexOptions.IgnoreCase);
            }

            Console.WriteLine($"Test expression after replacement: '{testExpression}'");

            // Try to evaluate the test expression to check syntax
            try
            {
                var result = EvaluateSimpleExpression(testExpression);
                Console.WriteLine($"Test expression evaluated successfully: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR evaluating test expression: {ex.Message}");
                return (false, $"Invalid arithmetic expression: {ex.Message}", null);
            }

            // Create calculator function
            Func<List<GenericCsvRow>, List<double>> calculator = (data) =>
            {
                var results = new List<double>();

                for (int i = 0; i < data.Count; i++)
                {
                    var row = data[i];
                    var rowExpression = expression;

                    // Replace each column reference with its value (considering range)
                    // Process longest column names first to avoid partial replacements
                    foreach (var colRef in columnReferences.OrderByDescending(c => c.Length))
                    {
                        // Use the actual column name from the mapping
                        var actualColumnName = columnMappings.ContainsKey(colRef) ? columnMappings[colRef] : colRef;
                        var value = GetNumericValueWithRange(row, actualColumnName, i);
                        var valueStr = value.ToString(System.Globalization.CultureInfo.InvariantCulture);

                        // Escape the column reference for regex and replace
                        // This handles special characters like brackets, dots, etc.
                        var escapedPattern = Regex.Escape(colRef);

                        // Replace all occurrences (already sorted by length to avoid partial matches)
                        // Use case-insensitive replacement
                        rowExpression = Regex.Replace(rowExpression, escapedPattern, valueStr, RegexOptions.IgnoreCase);
                    }

                    // Evaluate the expression
                    try
                    {
                        var result = EvaluateSimpleExpression(rowExpression);

                        // Check for division by zero or invalid results
                        if (double.IsInfinity(result) || double.IsNaN(result))
                        {
                            results.Add(0); // Skip division by zero - return 0
                        }
                        else
                        {
                            results.Add(result);
                        }
                    }
                    catch (DivideByZeroException)
                    {
                        // Skip division by zero errors - add 0 for this row
                        results.Add(0);
                    }
                    catch (Exception)
                    {
                        // For any other evaluation errors, add 0
                        results.Add(0);
                    }
                }

                return results;
            };

            return (true, "", calculator);
        }
        catch (Exception ex)
        {
            return (false, $"Error parsing arithmetic expression: {ex.Message}", null);
        }
    }

    // Simple expression evaluator (supports +, -, *, /, parentheses)
    private static double EvaluateSimpleExpression(string expression)
    {
        expression = expression.Trim();

        // Use DataTable.Compute for evaluation (supports +, -, *, /, (), etc.)
        try
        {
            var table = new System.Data.DataTable();
            var result = table.Compute(expression, null);
            return Convert.ToDouble(result);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to evaluate expression '{expression}': {ex.Message}", ex);
        }
    }

    // Split arguments by comma, respecting quotes
    private static List<string> SplitArguments(string argumentsString)
    {
        var arguments = new List<string>();
        var currentArg = new System.Text.StringBuilder();
        bool inQuotes = false;
        char quoteChar = '\0';

        for (int i = 0; i < argumentsString.Length; i++)
        {
            char c = argumentsString[i];

            if ((c == '"' || c == '\'') && (i == 0 || argumentsString[i - 1] != '\\'))
            {
                if (!inQuotes)
                {
                    inQuotes = true;
                    quoteChar = c;
                }
                else if (c == quoteChar)
                {
                    inQuotes = false;
                }
                currentArg.Append(c);
            }
            else if (c == ',' && !inQuotes)
            {
                arguments.Add(currentArg.ToString());
                currentArg.Clear();
            }
            else
            {
                currentArg.Append(c);
            }
        }

        if (currentArg.Length > 0)
        {
            arguments.Add(currentArg.ToString());
        }

        return arguments;
    }

    // Parse column reference with optional range: "Column1" or "Column1{10:100}"
    private static (string columnName, int? startRow, int? endRow) ParseColumnReference(string reference)
    {
        var match = Regex.Match(reference.Trim(), @"^([^\{]+)(?:\{(\d+):(\d+)\})?$");

        if (!match.Success)
        {
            return (reference.Trim(), null, null);
        }

        var columnName = match.Groups[1].Value.Trim();
        int? startRow = null;
        int? endRow = null;

        if (match.Groups[2].Success && match.Groups[3].Success)
        {
            startRow = int.Parse(match.Groups[2].Value);
            endRow = int.Parse(match.Groups[3].Value);
        }

        return (columnName, startRow, endRow);
    }

    // Apply range filter to row index
    private static bool IsRowInRange(int rowIndex, int? startRow, int? endRow)
    {
        if (startRow == null && endRow == null)
        {
            return true;
        }

        if (startRow != null && rowIndex < startRow.Value)
        {
            return false;
        }

        if (endRow != null && rowIndex > endRow.Value)
        {
            return false;
        }

        return true;
    }

    // Get numeric value from column reference with range support
    private static double GetNumericValueWithRange(GenericCsvRow row, string columnRef, int rowIndex)
    {
        var (colName, startRow, endRow) = ParseColumnReference(columnRef);

        if (!IsRowInRange(rowIndex, startRow, endRow))
        {
            return 0;
        }

        return row.GetNumericValue(colName);
    }

    // Get string value from column reference with range support
    private static string? GetValueWithRange(GenericCsvRow row, string columnRef, int rowIndex)
    {
        var (colName, startRow, endRow) = ParseColumnReference(columnRef);

        if (!IsRowInRange(rowIndex, startRow, endRow))
        {
            return null;
        }

        return row.GetValue(colName);
    }

    // Evaluate criteria for conditional functions (supports =, <, >, <=, >=, <>)
    private static bool EvaluateCriteria(string? value, string criteria)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        // Check for comparison operators
        if (criteria.StartsWith(">="))
        {
            var threshold = criteria.Substring(2).Trim();
            if (double.TryParse(value, out var numValue) && double.TryParse(threshold, out var numThreshold))
            {
                return numValue >= numThreshold;
            }
            return string.Compare(value, threshold, StringComparison.Ordinal) >= 0;
        }
        else if (criteria.StartsWith("<="))
        {
            var threshold = criteria.Substring(2).Trim();
            if (double.TryParse(value, out var numValue) && double.TryParse(threshold, out var numThreshold))
            {
                return numValue <= numThreshold;
            }
            return string.Compare(value, threshold, StringComparison.Ordinal) <= 0;
        }
        else if (criteria.StartsWith("<>"))
        {
            var compareValue = criteria.Substring(2).Trim();
            return !value.Equals(compareValue, StringComparison.OrdinalIgnoreCase);
        }
        else if (criteria.StartsWith(">"))
        {
            var threshold = criteria.Substring(1).Trim();
            if (double.TryParse(value, out var numValue) && double.TryParse(threshold, out var numThreshold))
            {
                return numValue > numThreshold;
            }
            return string.Compare(value, threshold, StringComparison.Ordinal) > 0;
        }
        else if (criteria.StartsWith("<"))
        {
            var threshold = criteria.Substring(1).Trim();
            if (double.TryParse(value, out var numValue) && double.TryParse(threshold, out var numThreshold))
            {
                return numValue < numThreshold;
            }
            return string.Compare(value, threshold, StringComparison.Ordinal) < 0;
        }
        else if (criteria.StartsWith("="))
        {
            var compareValue = criteria.Substring(1).Trim();
            return value.Equals(compareValue, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            // Default: exact match (case-insensitive)
            return value.Equals(criteria, StringComparison.OrdinalIgnoreCase);
        }
    }
}
