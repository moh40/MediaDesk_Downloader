using System.IO;

namespace MediaDesk.Models;

public sealed class AppState
{
    public string DownloadFolder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "MediaDesk");
    public bool IsArabic { get; set; } = true;
    public List<DownloadEntry> Downloads { get; set; } = [];
    public List<string> Favorites { get; set; } = [];
}
