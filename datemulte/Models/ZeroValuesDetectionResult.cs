namespace Datemulte_2.Models;

public class ZeroValueDetectionResult
{
    public bool HasZeros { get; set; }
    public Dictionary<string, int> ZeroCountByColumn { get; set; } = new();
    public int TotalRows { get; set; }
    public int TotalZeros { get; set; }
}