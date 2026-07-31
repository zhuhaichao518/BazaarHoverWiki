using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace BazaarHoverWiki;

public partial class WikiWindow : Window
{
    private readonly string _wikiSearchUrl;
    private Uri? _pendingTarget;
    private long _navigationSequence;

    public WikiWindow(string wikiSearchUrl)
    {
        InitializeComponent();
        _wikiSearchUrl = wikiSearchUrl;
        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs eventArgs)
    {
        var handle = new WindowInteropHelper(this).Handle;
        NativeMethods.ConfigureOverlayWindow(handle);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await Browser.EnsureCoreWebView2Async();
            Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            Browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            Browser.CoreWebView2.Settings.AreHostObjectsAllowed = false;
            Browser.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
            Browser.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
            Browser.CoreWebView2.NavigationStarting += Browser_OnNavigationStarting;
            NavigatePendingTarget();
        }
        catch (Exception exception)
        {
            LoadingPanel.Visibility = Visibility.Visible;
            TitleText.Text = $"WebView2 初始化失败：{exception.Message}";
        }
    }

    public void NavigateTo(string query)
    {
        query = query.Trim();
        if (query.Length == 0)
            return;

        TitleText.Text = query;
        LoadingPanel.Visibility = Visibility.Collapsed;
        var encoded = Uri.EscapeDataString(query);
        var url = _wikiSearchUrl.Replace("{query}", encoded, StringComparison.Ordinal);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var target) || target.Scheme != Uri.UriSchemeHttps)
        {
            LoadingPanel.Visibility = Visibility.Visible;
            TitleText.Text = "Wiki 地址无效：只允许 HTTPS";
            return;
        }
        _pendingTarget = target;
        NavigatePendingTarget();
    }

    private void NavigatePendingTarget()
    {
        if (_pendingTarget is null || Browser.CoreWebView2 is null)
            return;

        var target = _pendingTarget;
        _pendingTarget = null;
        var builder = new UriBuilder(target);
        var existingQuery = builder.Query.TrimStart('?');
        var separator = existingQuery.Length == 0 ? string.Empty : "&";
        builder.Query = $"{existingQuery}{separator}bhw={++_navigationSequence}";
        Browser.CoreWebView2.Navigate(builder.Uri.AbsoluteUri);
    }

    public void PositionBeside(System.Drawing.Point cursor)
    {
        var screen = System.Windows.Forms.Screen.FromPoint(cursor).WorkingArea;
        double targetLeft = cursor.X + 36;
        if (targetLeft + Width > screen.Right)
            targetLeft = cursor.X - Width - 36;

        var targetTop = Math.Clamp(cursor.Y - 110, screen.Top + 8, screen.Bottom - Height - 8);
        Left = Math.Max(screen.Left + 8, targetLeft);
        Top = targetTop;
    }

    private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Browser_OnNavigationStarting(
        object? sender,
        CoreWebView2NavigationStartingEventArgs eventArgs
    )
    {
        if (!Uri.TryCreate(eventArgs.Uri, UriKind.Absolute, out var target))
        {
            eventArgs.Cancel = true;
            return;
        }

        var allowed =
            target.Scheme == Uri.UriSchemeHttps
            && (
                target.Host.Equals("bazaardb.gg", StringComparison.OrdinalIgnoreCase)
                || target.Host.EndsWith(".bazaardb.gg", StringComparison.OrdinalIgnoreCase)
            );
        if (allowed)
            return;

        eventArgs.Cancel = true;
        TitleText.Text = "已阻止跳转到 BazaarDB 之外的网站";
    }
}
