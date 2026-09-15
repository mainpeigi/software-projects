namespace Datemulte_2.Models;

/// <summary>
/// Comprehensive session state model containing all data, UI settings, and chart customization options
/// Stores 160+ properties for complete application state including loaded data, visualization settings, and user preferences
/// Used for in-memory state management and can be persisted to database via SessionUIState
/// </summary>
public class SessionState
{
    // === Session Metadata ===
    // Unique identifier for this session
    public string Id { get; set; } = Guid.NewGuid().ToString();
    // User-defined session name
    public string Name { get; set; } = "Untitled Session";
    // Session creation timestamp
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // Last modification timestamp
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    // === Data Storage ===
    // All loaded data rows stored as generic dictionary-based objects
    public List<GenericCsvRow> Data { get; set; } = new();
    // List of all column names detected in the loaded file
    public List<string> AvailableColumns { get; set; } = new();
    // Columns identified as containing numeric data (for plotting)
    public List<string> NumericColumns { get; set; } = new();
    // Column identified or selected as the time/X-axis column
    public string? TimeColumn { get; set; }
    // Original filename of the loaded data
    public string? FileName { get; set; }
    // Timestamp when the file was last loaded
    public DateTime? LastFileLoadTimestamp { get; set; }
    // Size of the loaded file in bytes
    public long? LastFileSize { get; set; }
    // Flag indicating if file content is stored in the session
    public bool HasStoredFile { get; set; } = false;

    // === Chart Configuration ===
    // Columns selected for plotting on the primary Y-axis
    public HashSet<string> SelectedColumnsY1 { get; set; } = new();
    // Columns selected for plotting on the secondary Y-axis
    public HashSet<string> SelectedColumnsY2 { get; set; } = new();
    // Data sampling level (max points to display for performance)
    public int SamplingLevel { get; set; } = 5000;
    // Enable or disable secondary Y-axis
    public bool EnableY2Axis { get; set; } = false;
    // Chart layout mode: "single", "horizontal", "vertical", "grid"
    public string ChartLayout { get; set; } = "single";
    // Number of split charts when using split layout
    public int SplitLevel { get; set; } = 1;
    // Maps columns to specific chart indices in split view
    public Dictionary<string, int> ChartAssignments { get; set; } = new();
    // Start index for data range filtering
    public int RangeStartPoint { get; set; } = 0;
    // End index for data range filtering
    public int RangeEndPoint { get; set; } = 0;

    // === Calculated Columns ===
    // List of user-defined calculated column names
    public List<string> CalculatedColumns { get; set; } = new();
    // Formula definitions for calculated columns
    public Dictionary<string, CalculationFormula> CalculationFormulas { get; set; } = new();
    // Cached calculation results for performance
    public Dictionary<string, List<double>> CalculationCache { get; set; } = new();

    // === Column Metadata ===
    // Units of measurement for each column (e.g., "m/s", "°C")
    public Dictionary<string, string> ColumnUnits { get; set; } = new();
    // List of sensor group names (for grouped data formats)
    public List<string> SensorGroups { get; set; } = new();
    // Currently selected sensor group filter
    public string SelectedSensorGroup { get; set; } = "All";
    // Maps each column to its sensor group
    public Dictionary<string, string> ColumnToGroupMapping { get; set; } = new();

    // === Data Filtering ===
    // Active data filters applied to the dataset
    public List<DataFilterState> ActiveFilters { get; set; } = new();
    // Saved zoom start position (percentage 0-100)
    public double SavedZoomStart { get; set; } = 0;
    // Saved zoom end position (percentage 0-100)
    public double SavedZoomEnd { get; set; } = 100;

    // === Visual Styling ===
    // Chart theme: "dark" or "light"
    public string ChartTheme { get; set; } = "dark";
    // Color palette: "default", "vibrant", "pastel", "custom"
    public string ColorPalette { get; set; } = "default";
    // Custom color list when using custom palette
    public List<string> CustomColors { get; set; } = new();
    // Line style: "solid", "dashed", "dotted"
    public string LineStyle { get; set; } = "solid";
    // Line opacity (0-100)
    public int LineOpacity { get; set; } = 100;
    // Area fill opacity for area charts (0-100)
    public int AreaOpacity { get; set; } = 20;
    // Chart background color (hex or rgba)
    public string ChartBackgroundColor { get; set; } = "#ffffff";
    // Grid line color
    public string GridLineColor { get; set; } = "#e0e0e0";
    // Grid line width in pixels
    public int GridLineWidth { get; set; } = 1;
    // Grid line style: "solid", "dashed", "dotted"
    public string GridLineStyle { get; set; } = "solid";
    // Show X-axis line
    public bool ShowXAxisLine { get; set; } = true;
    // Show Y-axis line
    public bool ShowYAxisLine { get; set; } = true;
    // Axis line color
    public string AxisLineColor { get; set; } = "#333333";
    // Axis line width in pixels
    public int AxisLineWidth { get; set; } = 1;
    // Enable smooth/curved lines
    public bool EnableSmoothLines { get; set; } = false;
    // Show markers on data points
    public bool ShowDataPointMarkers { get; set; } = false;
    // Symbol type: "circle", "rect", "triangle", "diamond"
    public string SymbolType { get; set; } = "circle";
    // Symbol size in pixels
    public int SymbolSize { get; set; } = 8;
    // Default line width in pixels
    public int DefaultLineWidth { get; set; } = 2;

    // === Title Configuration ===
    // Show chart title
    public bool ShowTitle { get; set; } = true;
    // Chart title text
    public string TitleText { get; set; } = "Sensor Data Visualization";
    // Title text color
    public string TitleColor { get; set; } = "#333333";
    // Title font size in pixels
    public int TitleFontSize { get; set; } = 18;
    // Title font weight: "normal", "bold"
    public string TitleFontWeight { get; set; } = "bold";
    // Title horizontal alignment: "left", "center", "right"
    public string TitleAlignment { get; set; } = "center";
    // Show chart subtitle
    public bool ShowSubtitle { get; set; } = false;
    // Subtitle text
    public string SubtitleText { get; set; } = "";
    // Subtitle text color
    public string SubtitleColor { get; set; } = "#666666";
    // Subtitle font size in pixels
    public int SubtitleFontSize { get; set; } = 12;

    // === Legend Configuration ===
    // Show chart legend
    public bool ShowLegend { get; set; } = true;
    // Legend position: "top", "bottom", "left", "right"
    public string LegendPosition { get; set; } = "top";
    // Legend background color
    public string LegendBackgroundColor { get; set; } = "transparent";
    // Legend border color
    public string LegendBorderColor { get; set; } = "#cccccc";
    // Legend border width in pixels
    public int LegendBorderWidth { get; set; } = 0;
    // Legend text color
    public string LegendTextColor { get; set; } = "#333333";
    // Legend font size in pixels
    public int LegendFontSize { get; set; } = 12;

    // === Tooltip Configuration ===
    // Tooltip trigger: "axis" (show for all series) or "item" (show for hovered item only)
    public string TooltipTrigger { get; set; } = "axis";
    // Tooltip background color
    public string TooltipBackgroundColor { get; set; } = "rgba(50, 50, 50, 0.95)";
    // Tooltip border color
    public string TooltipBorderColor { get; set; } = "#20818e";
    // Tooltip border width in pixels
    public int TooltipBorderWidth { get; set; } = 2;
    // Tooltip text color
    public string TooltipTextColor { get; set; } = "#ffffff";
    // Tooltip font size in pixels
    public int TooltipFontSize { get; set; } = 14;

    // === X-Axis Configuration ===
    // X-axis label/name
    public string XAxisName { get; set; } = "Time";
    // X-axis name color
    public string XAxisNameColor { get; set; } = "#333333";
    // X-axis name font size in pixels
    public int XAxisNameFontSize { get; set; } = 14;
    // X-axis tick label color
    public string XAxisLabelColor { get; set; } = "#333333";
    // X-axis tick label font size in pixels
    public int XAxisLabelFontSize { get; set; } = 12;
    // X-axis label rotation in degrees
    public int XAxisLabelRotation { get; set; } = 0;
    // X-axis tick mark color
    public string XAxisTickColor { get; set; } = "#333333";
    // X-axis tick mark length in pixels
    public int XAxisTickLength { get; set; } = 5;
    // Enable auto-scaling for X-axis
    public bool XAxisAutoScale { get; set; } = true;
    // X-axis minimum value (when auto-scale is off)
    public double XAxisMin { get; set; } = 0;
    // X-axis maximum value (when auto-scale is off)
    public double XAxisMax { get; set; } = 100;

    // === Y-Axis Configuration (Primary) ===
    // Y-axis label/name
    public string YAxisName { get; set; } = "Value";
    // Y-axis name color
    public string YAxisNameColor { get; set; } = "#333333";
    // Y-axis name font size in pixels
    public int YAxisNameFontSize { get; set; } = 14;
    // Y-axis tick label color
    public string YAxisLabelColor { get; set; } = "#333333";
    // Y-axis tick label font size in pixels
    public int YAxisLabelFontSize { get; set; } = 12;
    // Y-axis tick mark color
    public string YAxisTickColor { get; set; } = "#333333";
    // Y-axis tick mark length in pixels
    public int YAxisTickLength { get; set; } = 5;
    // Enable auto-scaling for Y-axis
    public bool YAxisAutoScale { get; set; } = true;
    // Y-axis minimum value (when auto-scale is off)
    public double YAxisMin { get; set; } = 0;
    // Y-axis maximum value (when auto-scale is off)
    public double YAxisMax { get; set; } = 100;

    // === Y2-Axis Configuration (Secondary) ===
    // Y2-axis label/name
    public string Y2AxisName { get; set; } = "Value (Y2)";
    // Y2-axis name color
    public string Y2AxisNameColor { get; set; } = "#333333";
    // Y2-axis name font size in pixels
    public int Y2AxisNameFontSize { get; set; } = 14;
    // Y2-axis tick label color
    public string Y2AxisLabelColor { get; set; } = "#333333";
    // Y2-axis tick label font size in pixels
    public int Y2AxisLabelFontSize { get; set; } = 12;
    // Enable auto-scaling for Y2-axis
    public bool Y2AxisAutoScale { get; set; } = true;
    // Y2-axis minimum value (when auto-scale is off)
    public double Y2AxisMin { get; set; } = 0;
    // Y2-axis maximum value (when auto-scale is off)
    public double Y2AxisMax { get; set; } = 100;

    // === Grid Configuration ===
    // Show horizontal grid lines
    public bool ShowHorizontalGrid { get; set; } = true;
    // Show vertical grid lines
    public bool ShowVerticalGrid { get; set; } = false;
    // Grid top padding in pixels
    public int GridTop { get; set; } = 60;
    // Grid bottom padding in pixels
    public int GridBottom { get; set; } = 60;
    // Grid left padding in pixels
    public int GridLeft { get; set; } = 50;
    // Grid right padding in pixels
    public int GridRight { get; set; } = 50;
    // Grid area background color
    public string GridBackgroundColor { get; set; } = "transparent";
    // Grid border color
    public string GridBorderColor { get; set; } = "#cccccc";
    // Grid border width in pixels
    public int GridBorderWidth { get; set; } = 1;

    // === Data Zoom Configuration ===
    // Show slider-style data zoom control
    public bool ShowDataZoomSlider { get; set; } = true;
    // Enable inside data zoom (mouse wheel/pinch)
    public bool ShowDataZoomInside { get; set; } = true;
    // Data zoom slider height in pixels
    public int DataZoomHeight { get; set; } = 20;
    // Data zoom background color
    public string DataZoomBackgroundColor { get; set; } = "rgba(250, 10, 10, 0.1)";
    // Data zoom selected area fill color
    public string DataZoomFillerColor { get; set; } = "rgba(102, 126, 234, 0.2)";
    // Data zoom border color
    public string DataZoomBorderColor { get; set; } = "#cccccc";

    // === Toolbox Configuration ===
    // Show ECharts toolbox (feature controls)
    public bool ShowToolbox { get; set; } = true;
    // Enable save as image feature
    public bool ToolboxShowSaveAsImage { get; set; } = true;
    // Enable data zoom feature
    public bool ToolboxShowDataZoom { get; set; } = true;
    // Enable data view feature
    public bool ToolboxShowDataView { get; set; } = true;
    // Enable restore feature (reset chart)
    public bool ToolboxShowRestore { get; set; } = true;
    // Enable magic type feature (switch chart types)
    public bool ToolboxShowMagicType { get; set; } = true;
    // Toolbox icon color
    public string ToolboxIconColor { get; set; } = "#333333";
    // Toolbox icon hover color
    public string ToolboxEmphasisColor { get; set; } = "#20818e";

    // === Data Labels Configuration ===
    // Show labels on data points
    public bool ShowDataLabels { get; set; } = false;
    // Label position: "top", "bottom", "left", "right", "inside"
    public string DataLabelPosition { get; set; } = "top";
    // Label text color
    public string DataLabelColor { get; set; } = "#333333";
    // Label font size in pixels
    public int DataLabelFontSize { get; set; } = 12;
    // Label font weight: "normal", "bold"
    public string DataLabelFontWeight { get; set; } = "normal";
    // Label background color
    public string DataLabelBackgroundColor { get; set; } = "transparent";
    // Label border color
    public string DataLabelBorderColor { get; set; } = "transparent";
    // Label border width in pixels
    public int DataLabelBorderWidth { get; set; } = 0;
    // Label border radius in pixels
    public int DataLabelBorderRadius { get; set; } = 0;
    // Label padding in pixels
    public int DataLabelPadding { get; set; } = 0;
    // Label formatter: "value" or custom format
    public string DataLabelFormatter { get; set; } = "value";
    // Label rotation in degrees
    public int DataLabelRotate { get; set; } = 0;
    // Show labels only on hover
    public bool DataLabelShowOnHighlight { get; set; } = false;

    // === Chart Type ===
    // Default chart type: "line", "bar", "scatter", "area"
    public string DefaultChartType { get; set; } = "line";
}

/// <summary>
/// Represents a single data filter condition
/// Used to filter dataset rows based on column values and comparison operators
/// </summary>
public class DataFilterState
{
    // Unique filter identifier
    public string Id { get; set; } = "";
    // Column name to filter on
    public string Column { get; set; } = "";
    // Comparison operator: ">", "<", ">=", "<=", "==", "!="
    public string Operator { get; set; } = ">";
    // Filter value (string representation)
    public string Value { get; set; } = "";
    // Whether this filter is currently active
    public bool IsEnabled { get; set; } = true;
}