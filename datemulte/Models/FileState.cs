namespace Datemulte_2.Models;

public class FileState
{
    public string FileId { get; set; } = "";
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public DateTime LastSaved { get; set; }

    //Chart selections
    public List<string> SelectedColumnsY1 { get; set; } = new();
    public List<string> SelectedColumnsY2 { get; set; } = new();

    //Chart settings
    public int SamplingLevel { get; set; } = 1000;
    public bool EnableY2Axis { get; set; } = false;
    public string ChartLayout { get; set; } = "single";
    public int SplitLevel { get; set; } = 1;
    public Dictionary<string, int> ChartAssignments { get; set; } = new();

    public List<string> CalculatedColumns { get; set; } = new();
    public Dictionary<string, CalculationFormula> CalculationFormulas { get; set; } = new();

    //Data range
    public int RangeStartPoint { get; set; }
    public int RangeEndPoint { get; set; }

    //Column info
    public string? TimeColumn { get; set; }
    public List<string> NumericColumns { get; set; } = new();
    public Dictionary<string, string> ColumnUnits { get; set; } = new();

    //Sensor groups
    public List<string> SensorGroups { get; set; } = new();
    public string SelectedSensorGroup { get; set; } = "All";
    public Dictionary<string, string> ColumnToGroupMapping { get; set; } = new();
}