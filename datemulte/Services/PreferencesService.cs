using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Datemulte_2.Services;

public class PreferencesService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<PreferencesService> _logger;
    private const string STORAGE_KEY = "csvplotter_preferences";
    
    public event Action? OnPreferencesChanged;
    
    public PreferencesService(IJSRuntime jsRuntime, ILogger<PreferencesService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }
    
    public async Task<UserPreferences> LoadAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", STORAGE_KEY);
            
            _logger.LogDebug("LoadAsync - JSON from localStorage: {Json}", json);
            
            if (string.IsNullOrEmpty(json))
            {
                _logger.LogDebug("No preferences found, using defaults");
                return GetDefaultPreferences();
            }
            
            var preferences = JsonSerializer.Deserialize<UserPreferences>(json);
            _logger.LogDebug("Loaded preferences - IsDarkMode: {IsDarkMode}, ThemeColor: {ThemeColor}", 
                preferences?.IsDarkMode, preferences?.ThemeColorPreset);
            return preferences ?? GetDefaultPreferences();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading preferences");
            return GetDefaultPreferences();
        }
    }
    
    public async Task SaveAsync(UserPreferences preferences)
    {
        try
        {
            var json = JsonSerializer.Serialize(preferences);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", STORAGE_KEY, json);
            OnPreferencesChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving preferences");
        }
    }
    
    public UserPreferences GetDefaultPreferences()
    {
        return new UserPreferences
        {
            IsDarkMode = false,
            ThemeColorPreset = "purple",
            DefaultSamplingLevel = 1000,
            MaxFileSize = 200,
            ShowGridLines = true,
            EnableAnimations = true,
            DefaultChartLayout = "single",
            DecimalPlaces = 2,
            TemperatureUnit = "°C",
            DistanceUnit = "m",
            FlowUnit = "L/min",
            PressureUnit = "Pa",
            VoltageUnit = "V",
            CurrentUnit = "A",
            PowerUnit = "W",
            SpeedUnit = "m/s",
            ForceUnit = "N"
        };
    }
}

public class UserPreferences
{
    // Legacy properties (kept for compatibility)
    public bool IsDarkMode { get; set; }
    public string ThemeColorPreset { get; set; } = "purple";
    public int DefaultSamplingLevel { get; set; } = 1000;
    public int MaxFileSize { get; set; } = 200;
    public bool ShowGridLines { get; set; } = true;
    public bool EnableAnimations { get; set; } = true;
    public string DefaultChartLayout { get; set; } = "single";

    // CSV Export Settings
    public string? CsvDelimiter { get; set; } = ",";
    public bool IncludeHeaderInExport { get; set; } = true;
    public bool ExportOnlyVisibleColumns { get; set; } = false;

    // Data Display Settings
    public int DecimalPlaces { get; set; } = 2;
    public bool UseScientificNotation { get; set; } = false;
    public bool AutoDetectDateColumns { get; set; } = true;

    // Performance Settings
    public bool EnableAutoRefresh { get; set; } = false;
    public int ChartUpdateDebounce { get; set; } = 300;
    public bool EnableLazyLoading { get; set; } = true;
    public bool PerformanceMode { get; set; } = true;

    // Chart Appearance
    public int DefaultLineWidth { get; set; } = 2;
    public bool ShowDataPointMarkers { get; set; } = false;
    public bool EnableChartZoom { get; set; } = true;
    public string? DefaultChartType { get; set; } = "line";

    // Storage Management
    public bool RememberLastFile { get; set; } = true;
    public bool CacheCalculatedColumns { get; set; } = true;

    // Unit Preferences
    public string TemperatureUnit { get; set; } = "°C";
    public string DistanceUnit { get; set; } = "m";
    public string FlowUnit { get; set; } = "L/min";
    public string PressureUnit { get; set; } = "Pa";
    public string VoltageUnit { get; set; } = "V";
    public string CurrentUnit { get; set; } = "A";
    public string PowerUnit { get; set; } = "W";
    public string SpeedUnit { get; set; } = "m/s";
    public string ForceUnit { get; set; } = "N";

    // Advanced Chart Customization
    public string ChartTheme { get; set; } = "default";
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
    public string LegendPosition { get; set; } = "top";
    public string TooltipTrigger { get; set; } = "axis";
    public bool EnableSmoothLines { get; set; } = false;
    public int AnimationDuration { get; set; } = 1000;
    public string AnimationEasing { get; set; } = "cubicOut";
    public bool UseLogScale { get; set; } = false;
    public int SymbolSize { get; set; } = 8;
    public string SymbolType { get; set; } = "circle";

    // Per-Element Customization
    // Title
    public bool ShowTitle { get; set; } = true;
    public string TitleText { get; set; } = "Sensor Data Visualization";
    public string TitleColor { get; set; } = "#333333";
    public int TitleFontSize { get; set; } = 18;
    public string TitleFontWeight { get; set; } = "bold";
    public string TitleAlignment { get; set; } = "center";
    public int TitleTop { get; set; } = 10;
    public int TitleLeft { get; set; } = 50;

    // Subtitle
    public bool ShowSubtitle { get; set; } = false;
    public string SubtitleText { get; set; } = "";
    public string SubtitleColor { get; set; } = "#666666";
    public int SubtitleFontSize { get; set; } = 12;

    // Legend
    public bool ShowLegend { get; set; } = true;
    public string LegendBackgroundColor { get; set; } = "transparent";
    public string LegendBorderColor { get; set; } = "#cccccc";
    public int LegendBorderWidth { get; set; } = 0;
    public int LegendBorderRadius { get; set; } = 0;
    public string LegendTextColor { get; set; } = "#333333";
    public int LegendFontSize { get; set; } = 12;
    public int LegendItemGap { get; set; } = 10;
    public string LegendOrient { get; set; } = "horizontal";
    public int LegendPadding { get; set; } = 5;

    // Tooltip
    public string TooltipBackgroundColor { get; set; } = "rgba(50, 50, 50, 0.95)";
    public string TooltipBorderColor { get; set; } = "#667eea";
    public int TooltipBorderWidth { get; set; } = 2;
    public int TooltipBorderRadius { get; set; } = 4;
    public string TooltipTextColor { get; set; } = "#ffffff";
    public int TooltipFontSize { get; set; } = 14;
    public int TooltipPadding { get; set; } = 10;
    public string TooltipAxisPointerType { get; set; } = "cross";

    // X-Axis
    public string XAxisName { get; set; } = "";
    public string XAxisNameLocation { get; set; } = "middle";
    public int XAxisNameGap { get; set; } = 30;
    public string XAxisNameColor { get; set; } = "#333333";
    public int XAxisNameFontSize { get; set; } = 14;
    public string XAxisLabelColor { get; set; } = "#666666";
    public int XAxisLabelFontSize { get; set; } = 12;
    public int XAxisLabelRotate { get; set; } = 45;
    public int XAxisLabelMargin { get; set; } = 8;
    public bool XAxisShowTick { get; set; } = true;
    public string XAxisTickColor { get; set; } = "#333333";
    public int XAxisTickLength { get; set; } = 5;

    // Y-Axis (Primary)
    public string YAxisName { get; set; } = "";
    public string YAxisNameLocation { get; set; } = "middle";
    public int YAxisNameGap { get; set; } = 50;
    public string YAxisNameColor { get; set; } = "#333333";
    public int YAxisNameFontSize { get; set; } = 14;
    public string YAxisLabelColor { get; set; } = "#666666";
    public int YAxisLabelFontSize { get; set; } = 12;
    public bool YAxisShowTick { get; set; } = true;
    public string YAxisTickColor { get; set; } = "#333333";
    public int YAxisTickLength { get; set; } = 5;
    public int YAxisMin { get; set; } = 0;
    public int YAxisMax { get; set; } = 0;
    public bool YAxisAutoScale { get; set; } = true;

    // Y-Axis (Secondary)
    public string Y2AxisName { get; set; } = "";
    public string Y2AxisNameColor { get; set; } = "#333333";
    public int Y2AxisNameFontSize { get; set; } = 14;
    public string Y2AxisLabelColor { get; set; } = "#666666";
    public int Y2AxisLabelFontSize { get; set; } = 12;
    public int Y2AxisMin { get; set; } = 0;
    public int Y2AxisMax { get; set; } = 0;
    public bool Y2AxisAutoScale { get; set; } = true;

    // Grid
    public bool ShowHorizontalGrid { get; set; } = true;
    public bool ShowVerticalGrid { get; set; } = false;
    public int HorizontalGridInterval { get; set; } = 0; // 0 = auto
    public int VerticalGridInterval { get; set; } = 0; // 0 = auto
    public int GridTop { get; set; } = 60;
    public int GridBottom { get; set; } = 60;
    public int GridLeft { get; set; } = 50;
    public int GridRight { get; set; } = 50;
    public string GridBackgroundColor { get; set; } = "transparent";
    public string GridBorderColor { get; set; } = "#cccccc";
    public int GridBorderWidth { get; set; } = 1;

    // DataZoom
    public bool ShowDataZoomSlider { get; set; } = true;
    public bool ShowDataZoomInside { get; set; } = true;
    public bool ZoomSyncChart1 { get; set; } = false;
    public bool ZoomSyncChart2 { get; set; } = false;
    public bool ZoomSyncChart3 { get; set; } = false;
    public bool ZoomSyncChart4 { get; set; } = false;
    public bool ZoomSyncChart5 { get; set; } = false;
    public bool ZoomSyncChart6 { get; set; } = false;
    public bool ZoomSyncChart7 { get; set; } = false;
    public bool ZoomSyncChart8 { get; set; } = false;
    public bool ZoomSyncAllCharts { get; set; } = false;
    public string DataZoomBackgroundColor { get; set; } = "#f3f3f3";
    public string DataZoomFillerColor { get; set; } = "rgba(102, 126, 234, 0.2)";
    public string DataZoomBorderColor { get; set; } = "#cccccc";
    public int DataZoomHeight { get; set; } = 20;

    // Toolbox
    public bool ShowToolbox { get; set; } = true;
    public bool ToolboxShowSaveAsImage { get; set; } = true;
    public bool ToolboxShowDataZoom { get; set; } = true;
    public bool ToolboxShowDataView { get; set; } = false;
    public bool ToolboxShowRestore { get; set; } = true;
    public bool ToolboxShowMagicType { get; set; } = false;
    public string ToolboxIconColor { get; set; } = "#333333";
    public string ToolboxEmphasisColor { get; set; } = "#667eea";

    // Data Labels
    public bool ShowDataLabels { get; set; } = false;
    public string DataLabelPosition { get; set; } = "top";
    public string DataLabelColor { get; set; } = "#333333";
    public int DataLabelFontSize { get; set; } = 12;
    public string DataLabelFontWeight { get; set; } = "normal";
    public string DataLabelBackgroundColor { get; set; } = "transparent";
    public string DataLabelBorderColor { get; set; } = "transparent";
    public int DataLabelBorderWidth { get; set; } = 0;
    public int DataLabelBorderRadius { get; set; } = 2;
    public int DataLabelPadding { get; set; } = 3;
    public string DataLabelFormatter { get; set; } = "{c}";
    public int DataLabelRotate { get; set; } = 0;
    public bool DataLabelShowOnHighlight { get; set; } = false;

    // Series-specific overrides (stored as JSON)
    public Dictionary<string, SeriesCustomization> SeriesCustomizations { get; set; } = new();
}

public class SeriesCustomization
{
    public string? Color { get; set; }
    public string? LineStyle { get; set; }
    public int? LineWidth { get; set; }
    public int? LineOpacity { get; set; }
    public string? SymbolType { get; set; }
    public int? SymbolSize { get; set; }
    public bool? Smooth { get; set; }
    public int? AreaOpacity { get; set; }
    public int? ZIndex { get; set; }
}
