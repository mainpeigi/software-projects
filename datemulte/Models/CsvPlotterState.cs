namespace Datemulte_2.Models;

/// <summary>
/// Simplified plotter state model for CSV data visualization
/// Contains core data, chart configuration, and visual styling properties
/// Lighter alternative to SessionState for basic plotting scenarios
/// </summary>
public class CsvPlotterState
{
    // === Data Storage ===
    // All loaded data rows
    public List<GenericCsvRow> Data { get; set; } = new();
    // All available column names
    public List<string> AvailableColumns { get; set; } = new();
    // Columns containing numeric data
    public List<string> NumericColumns { get; set; } = new();
    // Time/X-axis column
    public string? TimeColumn { get; set; }
    // Loaded filename
    public string? FileName { get; set; }
    // File load timestamp
    public DateTime? LastFileLoadTimestamp { get; set; }
    // File size in bytes
    public long? LastFileSize { get; set; }

    // === Chart Configuration ===
    // Columns plotted on primary Y-axis
    public HashSet<string> SelectedColumnsY1 { get; set; } = new();
    // Columns plotted on secondary Y-axis
    public HashSet<string> SelectedColumnsY2 { get; set; } = new();

    // Maximum data points to render (for performance)
    public int SamplingLevel { get; set; } = 5000;
    // Enable secondary Y-axis
    public bool EnableY2Axis { get; set; } = false;

    // Chart layout: "single", "horizontal", "vertical", "grid"
    public string ChartLayout { get; set; } = "single";
    // Number of split charts
    public int SplitLevel { get; set; } = 1;
    // Maps columns to chart indices for split view
    public Dictionary<string, int> ChartAssignments { get; set; } = new();

    // Data range filtering
    public int RangeStartPoint { get; set; } = 0;
    public int RangeEndPoint { get; set; } = 0;

    // === Calculated Columns ===
    // List of calculated column names
    public List<string> CalculatedColumns { get; set; } = new();
    // Formula definitions for calculated columns
    public Dictionary<string, CalculationFormula> CalculationFormulas { get; set; } = new();
    // Cached calculation results
    public Dictionary<string, List<double>> CalculationCache { get; set; } = new();

    // Column units (e.g., "m/s", "°C")
    public Dictionary<string, string> ColumnUnits { get; set; } = new();

    // === Sensor Groups (for grouped CSV files) ===
    // List of sensor group names
    public List<string> SensorGroups { get; set; } = new();
    // Currently selected sensor group filter
    public string SelectedSensorGroup { get; set; } = "All";
    // Maps columns to their sensor groups
    public Dictionary<string, string> ColumnToGroupMapping { get; set; } = new();
    // === Visual Styling ===
    // Color scheme: "default", "vibrant", "pastel"
    public string ColorScheme { get; set; } = "default";
    // Line width in pixels
    public int LineWidth { get; set; } = 2;
    // Show markers on data points
    public bool ShowDataPointMarkers { get; set; } = false;
    // Line style: "solid", "dashed", "dotted"
    public string LineStyle { get; set; } = "solid";
    // Line opacity (0.0-1.0)
    public double LineOpacity { get; set; } = 1.0;
    // Enable chart animations
    public bool EnableChartAnimation { get; set; } = true;
    // Enable chart zoom functionality
    public bool EnableChartZoom { get; set; } = true;
    // Chart theme: "dark" or "light"
    public string ChartTheme { get; set; } = "dark";

    // === Y-Axis Configuration ===
    // Auto-scale Y-axis
    public bool YAxisAutoScale { get; set; } = true;
    // Y-axis minimum value (when auto-scale off)
    public double YAxisMin { get; set; } = 0;
    // Y-axis maximum value (when auto-scale off)
    public double YAxisMax { get; set; } = 100;
    // Auto-scale secondary Y-axis
    public bool Y2AxisAutoScale { get; set; } = true;
    // Secondary Y-axis minimum value
    public double Y2AxisMin { get; set; } = 0;
    // Secondary Y-axis maximum value
    public double Y2AxisMax { get; set; } = 100;

    // === Additional Chart Styling ===
    // Area chart fill opacity (0-100)
    public int AreaOpacity { get; set; } = 20;
    // Chart background color
    public string ChartBackgroundColor { get; set; } = "#ffffff";
    // Grid line color
    public string GridLineColor { get; set; } = "#e0e0e0";
    // Grid line width in pixels
    public int GridLineWidth { get; set; } = 1;
    // Grid line style
    public string GridLineStyle { get; set; } = "solid";
    // Show X-axis line
    public bool ShowXAxisLine { get; set; } = true;
    // Show Y-axis line
    public bool ShowYAxisLine { get; set; } = true;
    // Axis line color
    public string AxisLineColor { get; set; } = "#333333";
    // Axis line width in pixels
    public int AxisLineWidth { get; set; } = 1;

    // === Legend & Tooltip ===
    // Legend position: "top", "bottom", "left", "right"
    public string LegendPosition { get; set; } = "top";
    // Tooltip trigger: "axis" or "item"
    public string TooltipTrigger { get; set; } = "axis";

    // === Animation Settings ===
    // Enable smooth/curved lines
    public bool EnableSmoothLines { get; set; } = false;
    // Animation duration in milliseconds
    public int AnimationDuration { get; set; } = 1000;
    // Animation easing function
    public string AnimationEasing { get; set; } = "cubicOut";

    // === Symbol/Marker Settings ===
    // Symbol size in pixels
    public int SymbolSize { get; set; } = 8;
    // Symbol type: "circle", "rect", "triangle", "diamond"
    public string SymbolType { get; set; } = "circle";

    // === Scale Settings ===
    // Use logarithmic scale for Y-axis
    public bool UseLogScale { get; set; } = false;
}

/// <summary>
/// Defines a mathematical formula for calculated columns
/// Supports basic binary operations between two columns
/// </summary>
public class CalculationFormula
{
    // First operand column name
    public string Column1 { get; set; } = "";
    // Second operand column name
    public string Column2 { get; set; } = "";
    // Operation: "+", "-", "*", "/"
    public string Operation { get; set; } = "";
}
