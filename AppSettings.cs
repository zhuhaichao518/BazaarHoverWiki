using System.IO;
using System.Text.Json;

namespace BazaarHoverWiki;

public sealed class AppSettings
{
    public string WikiSearchUrl { get; set; } = "https://bazaardb.gg/search?q={query}";
    public int CaptureWidth { get; set; } = 1500;
    public int CaptureHeight { get; set; } = 900;
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
