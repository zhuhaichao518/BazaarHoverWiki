using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace BazaarHoverWiki;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly HoverOcrService _ocr;
    private readonly WikiWindow _wiki;
    private readonly DispatcherTimer _timer;
    private CancellationTokenSource? _scanCancellation;
    private bool _scannerEnabled = true;
    private bool _scanRunning;
    private string _pendingQuery = string.Empty;
    private string _shownQuery = string.Empty;
    private int _stableScans;

    public MainWindow()
    {
        InitializeComponent();
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
        _settings = AppSettings.Load(settingsPath);
        _ocr = new HoverOcrService(_settings.PreferredOcrLanguages);
        _wiki = new WikiWindow(_settings.WikiSearchUrl);
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Math.Clamp(_settings.ScanIntervalMs, 300, 5000)),
        };
        _timer.Tick += Timer_OnTick;

        GameOnlyCheckBox.IsChecked = _settings.OnlyWhenGameIsForeground;
        OcrLanguageText.Text = _ocr.ActiveLanguages.Count == 0
            ? "OCR：不可用"
            : $"OCR：{string.Join(", ", _ocr.ActiveLanguages)}";
        Loaded += OnLoaded;
        Closed += OnClosed;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _wiki.Owner = this;
        _wiki.Show();
        _wiki.Hide();

        if (_ocr.ActiveLanguages.Count == 0)
        {
            SetStatus("没有可用的 Windows OCR 语言包", false);
            _scannerEnabled = false;
            ToggleButton.Content = "继续识别";
            return;
        }

        SetStatus("自动识别已开启", true);
        _timer.Start();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WndProc);
        var handle = source?.Handle ?? IntPtr.Zero;
        NativeMethods.RegisterHotKey(handle, NativeMethods.HotkeyToggleScanner, NativeMethods.ModNone, NativeMethods.VkF8);
        NativeMethods.RegisterHotKey(
            handle,
            NativeMethods.HotkeyScanNow,
            NativeMethods.ModControl | NativeMethods.ModShift,
            NativeMethods.VkW
        );
        NativeMethods.RegisterHotKey(handle, NativeMethods.HotkeyToggleWikiInput, NativeMethods.ModNone, NativeMethods.VkF9);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _scanCancellation?.Cancel();
        var handle = new WindowInteropHelper(this).Handle;
        NativeMethods.UnregisterHotKey(handle, NativeMethods.HotkeyToggleScanner);
        NativeMethods.UnregisterHotKey(handle, NativeMethods.HotkeyScanNow);
        NativeMethods.UnregisterHotKey(handle, NativeMethods.HotkeyToggleWikiInput);
        _wiki.Close();
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != NativeMethods.WmHotkey)
            return IntPtr.Zero;

        handled = true;
        switch (wParam.ToInt32())
        {
            case NativeMethods.HotkeyToggleScanner:
                ToggleScanner();
                break;
            case NativeMethods.HotkeyScanNow:
                _ = ScanAsync(force: true);
                break;
            case NativeMethods.HotkeyToggleWikiInput:
                EnsureWikiVisible();
                _wiki.ToggleInteractive();
                break;
        }

        return IntPtr.Zero;
    }

    private async void Timer_OnTick(object? sender, EventArgs e) => await ScanAsync(force: false);

    private async Task ScanAsync(bool force)
    {
        if (_scanRunning || (!force && !_scannerEnabled))
            return;

        var foreground = NativeMethods.GetForegroundApp();
        ForegroundText.Text = $"前台窗口：{foreground.ProcessName} · {foreground.WindowTitle}";
        if (!force && GameOnlyCheckBox.IsChecked == true && !IsBazaar(foreground))
        {
            SetStatus("等待 The Bazaar 切到前台", true);
            return;
        }

        _scanRunning = true;
        _scanCancellation?.Cancel();
        _scanCancellation = new CancellationTokenSource();
        try
        {
            SetStatus("正在识别鼠标附近…", true);
            var frame = ScreenCapture.AroundCursor(_settings.CaptureWidth, _settings.CaptureHeight);
            var candidates = await _ocr.RecognizeNearCursorAsync(
                frame,
                _scanCancellation.Token
            );
            CandidateText.Text = candidates.Count == 0
                ? "候选：未识别到文字"
                : $"候选：{string.Join(" ｜ ", candidates.Take(4).Select(candidate => candidate.Text))}";

            var best = candidates.FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(best))
            {
                SetStatus("未识别到可搜索的名称", false);
                return;
            }

            QueryTextBox.Text = best;
            if (force)
            {
                ShowQuery(best);
                SetStatus($"已识别：{best}", true);
                return;
            }

            if (string.Equals(best, _pendingQuery, StringComparison.OrdinalIgnoreCase))
            {
                _stableScans++;
            }
            else
            {
                _pendingQuery = best;
                _stableScans = 1;
            }

            if (_stableScans >= Math.Max(1, _settings.MinimumStableScans))
                ShowQuery(best);
            SetStatus($"已识别：{best}", true);
        }
        catch (OperationCanceledException)
        {
            // A newer scan superseded this one.
        }
        catch (Exception exception)
        {
            SetStatus($"识别失败：{exception.Message}", false);
        }
        finally
        {
            _scanRunning = false;
        }
    }

    private bool IsBazaar(ForegroundApp foreground)
    {
        return _settings.ForegroundProcessNames.Any(
            expected =>
                foreground.ProcessName.Contains(expected, StringComparison.OrdinalIgnoreCase)
                || foreground.WindowTitle.Contains(expected, StringComparison.OrdinalIgnoreCase)
        );
    }

    private void ShowQuery(string query)
    {
        query = query.Trim();
        if (query.Length < 2)
            return;
        if (string.Equals(query, _shownQuery, StringComparison.OrdinalIgnoreCase))
            return;

        _shownQuery = query;
        EnsureWikiVisible();
        _wiki.NavigateTo(query);
    }

    private void EnsureWikiVisible()
    {
        if (!_wiki.IsVisible)
            _wiki.Show();
        _wiki.PositionBeside(Forms.Cursor.Position);
    }

    private void ToggleScanner()
    {
        _scannerEnabled = !_scannerEnabled;
        ToggleButton.Content = _scannerEnabled ? "暂停识别" : "继续识别";
        SetStatus(_scannerEnabled ? "自动识别已开启" : "自动识别已暂停", _scannerEnabled);
    }

    private void SetStatus(string text, bool healthy)
    {
        StatusText.Text = text;
        StatusDot.Fill = new SolidColorBrush(
            healthy
                ? System.Windows.Media.Color.FromRgb(103, 211, 145)
                : System.Windows.Media.Color.FromRgb(231, 111, 96)
        );
    }

    private void ToggleButton_OnClick(object sender, RoutedEventArgs e) => ToggleScanner();

    private void ShowWikiButton_OnClick(object sender, RoutedEventArgs e) => EnsureWikiVisible();

    private void SearchButton_OnClick(object sender, RoutedEventArgs e) => ShowQuery(QueryTextBox.Text);

    private void QueryTextBox_OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        ShowQuery(QueryTextBox.Text);
        e.Handled = true;
    }

    private void GameOnlyCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;
        _pendingQuery = string.Empty;
        _stableScans = 0;
    }
}
