using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BazaarHoverWiki;

internal static class NativeMethods
{
    public const int HotkeyToggleScanner = 0xB401;
    public const int HotkeyScanNow = 0xB402;
    public const int HotkeyToggleWikiInput = 0xB403;

    public const uint ModNone = 0x0000;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint VkF8 = 0x77;
    public const uint VkF9 = 0x78;
    public const uint VkW = 0x57;

    public const int WmHotkey = 0x0312;
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

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

    public static void SetOverlayInputMode(IntPtr handle, bool interactive)
    {
        if (handle == IntPtr.Zero)
            return;

        var style = GetWindowLong(handle, GwlExStyle);
        style |= WsExToolWindow;
        if (interactive)
        {
            style &= ~WsExTransparent;
            style &= ~WsExNoActivate;
        }
        else
        {
            style |= WsExTransparent;
            style |= WsExNoActivate;
        }

        SetWindowLong(handle, GwlExStyle, style);
    }
}

internal readonly record struct ForegroundApp(IntPtr Handle, string ProcessName, string WindowTitle)
{
    public static ForegroundApp Empty => new(IntPtr.Zero, string.Empty, string.Empty);
}
