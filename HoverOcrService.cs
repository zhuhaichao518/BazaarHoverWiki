using System.Text.RegularExpressions;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace BazaarHoverWiki;

internal sealed record OcrCandidate(string Text, double Distance, double Width, double Height);

internal sealed class HoverOcrService
{
    private readonly List<(string Label, OcrEngine Engine)> _engines = [];

    public IReadOnlyList<string> ActiveLanguages => _engines.Select(engine => engine.Label).ToArray();

    public HoverOcrService(IEnumerable<string> preferredLanguages)
    {
        foreach (var languageTag in preferredLanguages.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var language = new Language(languageTag);
                var engine = OcrEngine.TryCreateFromLanguage(language);
                if (engine is not null)
                    _engines.Add((languageTag, engine));
            }
            catch
            {
                // A missing Windows OCR language pack is expected on some systems.
            }
        }

        if (_engines.Count == 0)
        {
            var profileEngine = OcrEngine.TryCreateFromUserProfileLanguages();
            if (profileEngine is not null)
                _engines.Add(("Windows profile", profileEngine));
        }
    }

    public async Task<IReadOnlyList<OcrCandidate>> RecognizeNearCursorAsync(
        CaptureFrame frame,
        CancellationToken cancellationToken
    )
    {
        if (_engines.Count == 0)
            return [];

        using var randomAccessStream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(randomAccessStream))
        {
            writer.WriteBytes(frame.PngBytes);
            await writer.StoreAsync();
            writer.DetachStream();
        }

        randomAccessStream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied
        );

        var candidates = new Dictionary<string, OcrCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, engine) in _engines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await engine.RecognizeAsync(bitmap);
            foreach (var line in result.Lines)
            {
                if (line.Words.Count == 0)
                    continue;

                var left = line.Words.Min(word => word.BoundingRect.X);
                var top = line.Words.Min(word => word.BoundingRect.Y);
                var right = line.Words.Max(word => word.BoundingRect.X + word.BoundingRect.Width);
                var bottom = line.Words.Max(word => word.BoundingRect.Y + word.BoundingRect.Height);
                var centerX = (left + right) / 2;
                var centerY = (top + bottom) / 2;
                var distance = Math.Sqrt(
                    Math.Pow(centerX - frame.CursorInFrame.X, 2)
                    + Math.Pow(centerY - frame.CursorInFrame.Y, 2)
                );

                var text = Normalize(line.Text);
                if (!IsUseful(text))
                    continue;

                var candidate = new OcrCandidate(text, distance, right - left, bottom - top);
                if (
                    !candidates.TryGetValue(text, out var existing)
                    || candidate.Distance < existing.Distance
                )
                {
                    candidates[text] = candidate;
                }
            }
        }

        return candidates.Values
            .OrderBy(candidate => candidate.Distance)
            .ThenByDescending(candidate => candidate.Width)
            .Take(8)
            .ToArray();
    }

    private static string Normalize(string value)
    {
        value = Regex.Replace(value, @"\s+", " ").Trim();
        value = value.Trim('|', '•', '·', '-', '_', ':', ';', ',', '.', '“', '”', '"', '\'', '[', ']');
        return value;
    }

    private static bool IsUseful(string text)
    {
        if (text.Length < 2 || text.Length > 80)
            return false;
        if (text.All(character => char.IsDigit(character) || char.IsPunctuation(character)))
            return false;

        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Buy",
            "Sell",
            "Leave",
            "Reroll",
            "Gold",
            "Day",
            "Skip",
            "购买",
            "出售",
            "离开",
            "跳过",
            "刷新",
        };
        return !ignored.Contains(text);
    }
}
