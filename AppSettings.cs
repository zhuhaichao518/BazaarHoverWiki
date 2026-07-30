using System.IO;
using System.Text.Json;

namespace BazaarHoverWiki;

public sealed class AppSettings
{
    public string WikiSearchUrl { get; set; } = "https://bazaardb.gg/search?q={query}";
    public int ScanIntervalMs { get; set; } = 650;
    public int CaptureWidth { get; set; } = 760;
    public int CaptureHeight { get; set; } = 320;
    public int MinimumStableScans { get; set; } = 2;
    public bool OnlyWhenGameIsForeground { get; set; } = true;
    public string[] ForegroundProcessNames { get; set; } = ["TheBazaar", "The Bazaar"];
    public string[] PreferredOcrLanguages { get; set; } = ["en-US", "zh-Hans"];

    public static AppSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new AppSettings();

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(
                       json,
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                   )
                   ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }
}
