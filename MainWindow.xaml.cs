using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace BazaarHoverWiki;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly HoverOcrService _ocr;
    private readonly WikiWindow _wiki;
    private CancellationTokenSource? _scanCancellation;
    private bool _scanRunning;
    private bool _scanPending;
    private bool _wikiPositioned;
    private bool _pluginEnabled = true;
    private IntPtr _windowHandle;

    public MainWindow()
    {
        InitializeComponent();
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
        _settings = AppSettings.Load(settingsPath);
        _ocr = new HoverOcrService(_settings.PreferredOcrLanguages);
        _wiki = new WikiWindow(_settings.WikiSearchUrl);

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
            return;
        }
        if (!_pluginEnabled)
        {
            SetStatus("F/D 快捷键注册失败 · 按 F9 重试", false);
            return;
        }

        SetStatus("手动识别已就绪 · 按 F 开始", true);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WndProc);
        _windowHandle = source?.Handle ?? IntPtr.Zero;
        _pluginEnabled = RegisterActionHotkeys();
        NativeMethods.RegisterHotKey(
            _windowHandle,
            NativeMethods.HotkeyTogglePlugin,
            NativeMethods.ModNoRepeat,
            NativeMethods.VkF9
        );
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _scanCancellation?.Cancel();
        UnregisterActionHotkeys();
        NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.HotkeyTogglePlugin);
        _wiki.Close();
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != NativeMethods.WmHotkey)
            return IntPtr.Zero;

        handled = true;
        switch (wParam.ToInt32())
        {
            case NativeMethods.HotkeyScanNow:
                TriggerScan();
                break;
            case NativeMethods.HotkeyToggleWikiWindow:
                ToggleWikiWindow();
                break;
            case NativeMethods.HotkeyTogglePlugin:
                TogglePlugin();
                break;
        }

        return IntPtr.Zero;
    }

    private bool RegisterActionHotkeys()
    {
        var scanRegistered = NativeMethods.RegisterHotKey(
            _windowHandle,
            NativeMethods.HotkeyScanNow,
            NativeMethods.ModNoRepeat,
            NativeMethods.VkF
        );
        var wikiRegistered = NativeMethods.RegisterHotKey(
            _windowHandle,
            NativeMethods.HotkeyToggleWikiWindow,
            NativeMethods.ModNoRepeat,
            NativeMethods.VkD
        );

        if (scanRegistered && wikiRegistered)
            return true;

        UnregisterActionHotkeys();
        return false;
    }

    private void UnregisterActionHotkeys()
    {
        NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.HotkeyScanNow);
        NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.HotkeyToggleWikiWindow);
    }

    private void TogglePlugin()
    {
        if (_pluginEnabled)
        {
            _pluginEnabled = false;
            _scanPending = false;
            _scanCancellation?.Cancel();
            UnregisterActionHotkeys();
            _wiki.Hide();
            SetStatus("插件已暂停 · 按 F9 恢复", false);
            return;
        }

        _pluginEnabled = RegisterActionHotkeys();
        SetStatus(
            _pluginEnabled ? "插件已恢复 · 按 F 搜索" : "F/D 快捷键注册失败",
            _pluginEnabled
        );
    }

    private void TriggerScan()
    {
        if (_ocr.ActiveLanguages.Count == 0)
        {
            SetStatus("没有可用的 Windows OCR 语言包", false);
            return;
        }

        if (_scanRunning)
        {
            _scanPending = true;
            _scanCancellation?.Cancel();
            return;
        }

        _ = ScanAsync();
    }

    private void ToggleWikiWindow()
    {
        if (_wiki.IsVisible)
        {
            _scanPending = false;
            _scanCancellation?.Cancel();
            _wiki.Hide();
            SetStatus("Wiki 已隐藏 · 按 D 重新显示", true);
            return;
        }

        EnsureWikiVisible();
        SetStatus("Wiki 已显示 · 可直接拖动和缩放", true);
    }

    private async Task ScanAsync()
    {
        if (_scanRunning)
            return;

        var foreground = NativeMethods.GetForegroundApp();
        ForegroundText.Text = $"前台窗口：{foreground.ProcessName} · {foreground.WindowTitle}";

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
            if (_scanPending)
            {
                _scanPending = false;
                _ = ScanAsync();
            }
        }
    }

    private void ShowQuery(string query)
    {
        query = query.Trim();
        if (query.Length < 2)
            return;

        EnsureWikiVisible();
        _wiki.NavigateTo(query);
    }

    private void EnsureWikiVisible()
    {
        if (!_wiki.IsVisible)
            _wiki.Show();
        if (!_wikiPositioned)
        {
            _wiki.PositionBeside(Forms.Cursor.Position);
            _wikiPositioned = true;
        }
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

    private void ShowWikiButton_OnClick(object sender, RoutedEventArgs e) => EnsureWikiVisible();

    private void SearchButton_OnClick(object sender, RoutedEventArgs e) => ShowQuery(QueryTextBox.Text);

    private void QueryTextBox_OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        ShowQuery(QueryTextBox.Text);
        e.Handled = true;
    }

}
