using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CodexWidget;

internal sealed class HotkeyManager : IDisposable
{
    private readonly IntPtr handle;
    private readonly HwndSource source;
    public event Action<int>? Pressed;

    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;

    public HotkeyManager(Window window)
    {
        handle = new WindowInteropHelper(window).Handle;
        source = HwndSource.FromHwnd(handle);
        source.AddHook(Hook);
        RegisterHotKey(handle, 1, ModControl | ModAlt, 0x55); // U show/hide
        RegisterHotKey(handle, 2, ModControl | ModAlt, 0x4C); // L unlock
        RegisterHotKey(handle, 3, ModControl | ModAlt, 0x52); // R refresh
        RegisterHotKey(handle, 4, ModControl | ModAlt, 0x53); // S settings
        RegisterHotKey(handle, 5, ModControl | ModAlt, 0x51); // Q quit
        RegisterHotKey(handle, 6, ModControl | ModAlt | ModShift, 0x7B); // F12 hard unlock
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey)
        {
            Pressed?.Invoke(wParam.ToInt32());
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        for (var i = 1; i <= 6; i++) UnregisterHotKey(handle, i);
        source.RemoveHook(Hook);
    }

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
