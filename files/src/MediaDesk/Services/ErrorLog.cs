using System.IO;
using System.Text.RegularExpressions;

namespace MediaDesk.Services;

/// <summary>Best-effort diagnostic logging, never throws into the UI thread.</summary>
public static class ErrorLog
{
    public static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MediaDesk", "errors.log");

    public static void Record(string operation, Exception exception)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (directory is null) return;
            Directory.CreateDirectory(directory);
            string content = $"[{DateTimeOffset.Now:O}] {operation}\n{exception}\n---\n";
            // URLs may contain private access tokens; exclude query/fragment from logs.
            content = Regex.Replace(content, @"https?://[^\s<>]+", match =>
            {
                var candidate = match.Value;
                int index = candidate.IndexOfAny(['?', '#']);
                return index >= 0 ? candidate[..index] + "[query-hidden]" : candidate;
            }, RegexOptions.IgnoreCase);
            File.AppendAllText(LogPath, content);
        }
        catch { /* Logging must never crash the application. */ }
    }
}
