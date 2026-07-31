using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BazaarHoverWiki;

internal static class NativeMethods
{
    public const int HotkeyScanNow = 0xB402;
    public const int HotkeyToggleWikiWindow = 0xB404;
    public const int HotkeyTogglePlugin = 0xB405;

    public const uint ModNoRepeat = 0x4000;
    public const uint VkD = 0x44;
    public const uint VkF = 0x46;
    public const uint VkF9 = 0x78;

    public const int WmHotkey = 0x0312;
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const uint WdaExcludeFromCapture = 0x00000011;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int index, int newStyle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);

    public static ForegroundApp GetForegroundApp()
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero)
            return ForegroundApp.Empty;

        GetWindowThreadProcessId(handle, out var processId);
        var titleBuffer = new StringBuilder(512);
        GetWindowText(handle, titleBuffer, titleBuffer.Capacity);

        try
        {
            var process = Process.GetProcessById((int)processId);
            return new ForegroundApp(handle, process.ProcessName, titleBuffer.ToString());
        }
        catch
        {
            return new ForegroundApp(handle, string.Empty, titleBuffer.ToString());
        }
    }

    public static bool ConfigureOverlayWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            return false;

        var style = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, style | WsExToolWindow | WsExNoActivate);
        return SetWindowDisplayAffinity(handle, WdaExcludeFromCapture);
    }
}

internal readonly record struct ForegroundApp(IntPtr Handle, string ProcessName, string WindowTitle)
{
    public static ForegroundApp Empty => new(IntPtr.Zero, string.Empty, string.Empty);
}
