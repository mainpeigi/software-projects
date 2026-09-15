namespace Datemulte_2.Models.DataManagement;

/// <summary>
/// Stores UI-specific session state (chart settings, filters, zoom levels, etc.).
/// Can be stored as JSON in a separate table or as a column in DataSessions.
/// </summary>
public class SessionUIState
{
    public Guid SessionId { get; set; }

    // Chart selections
    public HashSet<string> SelectedColumnsY1 { get; set; } = new();
    public HashSet<string> SelectedColumnsY2 { get; set; } = new();
    public int SamplingLevel { get; set; } = 5000;
    public bool EnableY2Axis { get; set; } = false;
    public string ChartLayout { get; set; } = "single";
    public int SplitLevel { get; set; } = 1;
    public Dictionary<string, int> ChartAssignments { get; set; } = new();

    // Data range and filters
    public int RangeStartPoint { get; set; } = 0;
    public int RangeEndPoint { get; set; } = 0;
    public List<DataFilterState> ActiveFilters { get; set; } = new();
    public double SavedZoomStart { get; set; } = 0;
    public double SavedZoomEnd { get; set; } = 100;

    // Chart appearance
    public string ChartTheme { get; set; } = "dark";
    public string ChartType { get; set; } = "line";
    public string ColorPalette { get; set; } = "default";
    public List<string> CustomColors { get; set; } = new();
    public string LineStyle { get; set; } = "solid";
    public int LineOpacity { get; set; } = 100;
    public int AreaOpacity { get; set; } = 20;
    public string ChartBackgroundColor { get; set; } = "#ffffff";
    public string GridLineColor { get; set; } = "#e0e0e0";
    public int GridLineWidth { get; set; } = 1;
    public string GridLineStyle { get; set; } = "solid";
    public bool ShowXAxisLine { get; set; } = true;
    public bool ShowYAxisLine { get; set; } = true;
    public string AxisLineColor { get; set; } = "#333333";
    public int AxisLineWidth { get; set; } = 1;
    public bool EnableSmoothLines { get; set; } = false;
    public bool ShowDataPointMarkers { get; set; } = false;
    public string SymbolType { get; set; } = "circle";
    public int SymbolSize { get; set; } = 8;
    public int DefaultLineWidth { get; set; } = 2;

    // Title settings
    public bool ShowTitle { get; set; } = true;
    public string TitleText { get; set; } = "Sensor Data Visualization";
    public string TitleColor { get; set; } = "#333333";
    public int TitleFontSize { get; set; } = 18;
    public string TitleFontWeight { get; set; } = "bold";
    public string TitleAlignment { get; set; } = "center";
    public int TitleTop { get; set; } = 10;
    public int TitleLeft { get; set; } = 0;
    public bool ShowSubtitle { get; set; } = false;
    public string SubtitleText { get; set; } = "";
    public string SubtitleColor { get; set; } = "#666666";
    public int SubtitleFontSize { get; set; } = 12;

    // Legend settings
    public bool ShowLegend { get; set; } = true;
    public string LegendPosition { get; set; } = "top";
    public string LegendOrient { get; set; } = "horizontal";
    public string LegendBackgroundColor { get; set; } = "transparent";
    public string LegendBorderColor { get; set; } = "#cccccc";
    public int LegendBorderWidth { get; set; } = 0;
    public int LegendBorderRadius { get; set; } = 0;
    public int LegendPadding { get; set; } = 5;
    public int LegendItemGap { get; set; } = 10;
    public string LegendTextColor { get; set; } = "#333333";
    public int LegendFontSize { get; set; } = 12;

    // Tooltip settings
    public string TooltipTrigger { get; set; } = "axis";
    public string TooltipBackgroundColor { get; set; } = "rgba(50, 50, 50, 0.95)";
    public string TooltipBorderColor { get; set; } = "#20818e";
    public int TooltipBorderWidth { get; set; } = 2;
    public string TooltipTextColor { get; set; } = "#ffffff";
    public int TooltipFontSize { get; set; } = 14;

    // Axis settings
    public string XAxisName { get; set; } = "Time";
    public string XAxisNameLocation { get; set; } = "middle";
    public int XAxisNameGap { get; set; } = 30;
    public string XAxisNameColor { get; set; } = "#333333";
    public int XAxisNameFontSize { get; set; } = 14;
    public string XAxisLabelColor { get; set; } = "#333333";
    public int XAxisLabelFontSize { get; set; } = 12;
    public int XAxisLabelRotation { get; set; } = 0;
    public int XAxisLabelMargin { get; set; } = 8;
    public int XAxisTickLength { get; set; } = 5;
    public string XAxisTickColor { get; set; } = "#333333";
    public bool XAxisAutoScale { get; set; } = true;
    public double XAxisMin { get; set; } = 0;
    public double XAxisMax { get; set; } = 100;

    public string YAxisName { get; set; } = "Value";
    public string YAxisNameLocation { get; set; } = "middle";
    public int YAxisNameGap { get; set; } = 50;
    public string YAxisNameColor { get; set; } = "#333333";
    public int YAxisNameFontSize { get; set; } = 14;
    public string YAxisLabelColor { get; set; } = "#333333";
    public int YAxisLabelFontSize { get; set; } = 12;
    public int YAxisTickLength { get; set; } = 5;
    public string YAxisTickColor { get; set; } = "#333333";
    public bool YAxisAutoScale { get; set; } = true;
    public double YAxisMin { get; set; } = 0;
    public double YAxisMax { get; set; } = 100;

    public string Y2AxisName { get; set; } = "Value (Y2)";
    public string Y2AxisNameColor { get; set; } = "#333333";
    public int Y2AxisNameFontSize { get; set; } = 14;
    public string Y2AxisLabelColor { get; set; } = "#333333";
    public int Y2AxisLabelFontSize { get; set; } = 12;
    public bool Y2AxisAutoScale { get; set; } = true;
    public double Y2AxisMin { get; set; } = 0;
    public double Y2AxisMax { get; set; } = 100;

    // Grid settings
    public bool ShowHorizontalGrid { get; set; } = true;
    public bool ShowVerticalGrid { get; set; } = false;
    public int GridTop { get; set; } = 60;
    public int GridBottom { get; set; } = 60;
    public int GridLeft { get; set; } = 50;
    public int GridRight { get; set; } = 50;
    public string GridBackgroundColor { get; set; } = "transparent";
    public string GridBorderColor { get; set; } = "#cccccc";
    public int GridBorderWidth { get; set; } = 1;

    // Data zoom settings
    public bool ShowDataZoomSlider { get; set; } = true;
    public bool ShowDataZoomInside { get; set; } = true;
    public int DataZoomHeight { get; set; } = 20;
    public string DataZoomBackgroundColor { get; set; } = "rgba(250, 10, 10, 0.1)";
    public string DataZoomFillerColor { get; set; } = "rgba(102, 126, 234, 0.2)";
    public string DataZoomBorderColor { get; set; } = "#cccccc";

    // Toolbox settings
    public bool ShowToolbox { get; set; } = true;
    public bool ToolboxShowSaveAsImage { get; set; } = true;
    public bool ToolboxShowDataZoom { get; set; } = true;
    public bool ToolboxShowDataView { get; set; } = true;
    public bool ToolboxShowRestore { get; set; } = true;
    public bool ToolboxShowMagicType { get; set; } = true;
    public string ToolboxIconColor { get; set; } = "#333333";
    public string ToolboxEmphasisColor { get; set; } = "#20818e";

    // Data label settings
    public bool ShowDataLabels { get; set; } = false;
    public string DataLabelPosition { get; set; } = "top";
    public string DataLabelColor { get; set; } = "#333333";
    public int DataLabelFontSize { get; set; } = 12;

    // Calculated columns
    public List<string> CalculatedColumns { get; set; } = new();
    public Dictionary<string, CalculationFormula> CalculationFormulas { get; set; } = new();

    // Sensor groups
    public string SelectedSensorGroup { get; set; } = "All";

    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}
