using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace BazaarHoverWiki;

internal sealed record OcrCandidate(
    string Text,
    double TitleScore,
    double Distance,
    double Width,
    double Height,
    bool IsTitleLike,
    double BlueRatio
);

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
        var colorMap = FrameColorMap.FromPng(frame.PngBytes);

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

                var rawText = line.Text;
                var text = Normalize(rawText);
                if (!IsUseful(text))
                    continue;

                var blueRatio = colorMap.GetBlueRatio(left, top, right, bottom);
                if (blueRatio >= 0.45)
                    continue;

                var isTitleLike = LooksLikeItemName(text, rawText);
                var titleScore = CalculateTitleScore(
                    text,
                    rawText,
                    centerY,
                    right - left,
                    bottom - top,
                    distance,
                    isTitleLike,
                    blueRatio,
                    frame
                );
                var candidate = new OcrCandidate(
                    text,
                    titleScore,
                    distance,
                    right - left,
                    bottom - top,
                    isTitleLike,
                    blueRatio
                );
                if (
                    !candidates.TryGetValue(text, out var existing)
                    || candidate.TitleScore > existing.TitleScore
                )
                {
                    candidates[text] = candidate;
                }
            }
        }

        var candidateValues = candidates.Values.ToArray();
        var largestTitleHeight = candidateValues
            .Where(candidate => candidate.IsTitleLike)
            .Select(candidate => candidate.Height)
            .DefaultIfEmpty(1)
            .Max();

        return candidateValues
            .OrderByDescending(
                candidate =>
                    candidate.TitleScore
                    + (candidate.IsTitleLike ? 220 * candidate.Height / largestTitleHeight : 0)
            )
            .ThenBy(candidate => candidate.Distance)
            .ThenByDescending(candidate => candidate.Width)
            .Take(8)
            .ToArray();
    }

    private static double CalculateTitleScore(
        string text,
        string rawText,
        double centerY,
        double width,
        double height,
        double distance,
        bool isTitleLike,
        double blueRatio,
        CaptureFrame frame
    )
    {
        var verticalDelta = centerY - frame.CursorInFrame.Y;
        var liesAboveCursor = verticalDelta < -frame.Height * 0.02;
        var containsDigit = text.Any(char.IsDigit);
        var containsSentencePunctuation = rawText.Any(
            character => "，。！？；：,.!?;:".Contains(character)
        );

        var score = height * 6.5 + Math.Min(width, 600) * 0.12 - distance * 0.05;
        score += liesAboveCursor ? 90 : -20;
        score += isTitleLike ? 420 : -260;
        score -= blueRatio * 500;
        score += containsDigit ? -130 : 100;
        score += containsSentencePunctuation ? -90 : 20;

        if (text.Length is >= 2 and <= 24)
            score += 100;
        else if (text.Length > 36)
            score -= 120;

        if (text.Length is >= 2 and <= 12 && text.Any(IsCjk))
            score += 45;
        if (LooksLikeDescription(text))
            score -= 180;

        if (verticalDelta < 0)
            score += Math.Min(-verticalDelta, frame.Height * 0.65) * 0.12;

        return score;
    }

    private static bool LooksLikeItemName(string text, string rawText)
    {
        if (text.Length is < 2 or > 28)
            return false;
        if (text.Any(char.IsDigit) || LooksLikeDescription(text))
            return false;

        return !rawText.Any(character => "，。！？；：,.!?;:→+×%".Contains(character));
    }

    private static string Normalize(string value)
    {
        return new string(value.Where(char.IsLetterOrDigit).ToArray());
    }

    private static bool IsUseful(string text)
    {
        if (text.Length < 2 || text.Length > 80)
            return false;
        if (text.All(character => char.IsDigit(character) || char.IsPunctuation(character)))
            return false;

        return !IgnoredLabels.Contains(text) && !ContainsOnlyInterfaceTags(text);
    }

    private static bool LooksLikeDescription(string text)
    {
        string[] prefixes =
        [
            "造成",
            "使用",
            "当你",
            "每当",
            "如果",
            "为非",
            "充能",
            "获得",
            "你的",
            "此物品",
            "相邻",
            "多重触发",
            "冷却时间",
            "Deal",
            "When",
            "Whenever",
            "While",
            "If",
            "Use",
            "Charge",
            "Gain",
            "Your",
            "This",
            "Adjacent",
            "Multicast",
        ];
        return prefixes.Any(prefix => text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsOnlyInterfaceTags(string text)
    {
        var remainder = text;
        foreach (var tag in InterfaceTags.OrderByDescending(tag => tag.Length))
            remainder = remainder.Replace(tag, string.Empty, StringComparison.OrdinalIgnoreCase);
        return remainder.Length == 0;
    }

    private static bool IsCjk(char character) =>
        character is >= '\u3400' and <= '\u9fff';

    private static readonly HashSet<string> IgnoredLabels = new(StringComparer.OrdinalIgnoreCase)
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
        "奖励",
    };

    private static readonly string[] InterfaceTags =
    [
        "Medium",
        "Weapon",
        "Property",
        "Vehicle",
        "Aquatic",
        "Dinosaur",
        "Small",
        "Large",
        "Shield",
        "Burn",
        "Poison",
        "Friend",
        "Tech",
        "Tool",
        "Drone",
        "Ammo",
        "中型",
        "小型",
        "大型",
        "武器",
        "科技",
        "工具",
        "地产",
        "载具",
        "无人机",
        "水系",
        "恐龙",
        "朋友",
        "护盾",
        "燃烧",
        "剧毒",
        "弹药",
    ];
}
