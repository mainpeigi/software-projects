namespace Datemulte_2.Models;

public class RecentFileInfo
{
    public string FileId { get; set; } = "";
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public DateTime LastAccessed { get; set; }
}

