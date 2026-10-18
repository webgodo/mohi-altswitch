using System.Runtime.InteropServices;
using MohiAltSwitch;

namespace TestWindows;

/// <summary>Three windows titled SWTEST-A/B/C. Created C, B, A so that A ends up on top and focused.</summary>
internal static class Program
{
    private const string ClassName = "MohiAltSwitch.TestWindow";
    private static readonly Native.WndProc s_wndProc = WndProc;

    private static int Main()
    {
        nint instance = Native.GetModuleHandleW(null);
        var wc = new Native.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<Native.WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProc),
            hInstance = instance,
            hCursor = Native.LoadCursorW(0, 32512),
            hbrBackground = 6,                                    // COLOR_WINDOW + 1
            lpszClassName = ClassName,
        };
        Native.RegisterClassExW(ref wc);

        nint last = 0;
        int i = 0;
        foreach (string label in new[] { "C", "B", "A" })
        {
            last = Native.CreateWindowExW(0, ClassName, "SWTEST-" + label, Native.WS_OVERLAPPEDWINDOW | Native.WS_VISIBLE,
                100 + 60 * ++i, 100 + 60 * i, 360, 200, 0, 0, instance, 0);
        }
        Native.SetForegroundWindow(last);
        Native.SetTimer(last, 1, 90_000, 0);                      // safety net: never outlive a forgotten test run

        while (Native.GetMessageW(out var msg, 0, 0, 0) > 0)
        {
            Native.TranslateMessage(ref msg);
            Native.DispatchMessageW(ref msg);
        }
        return 0;
    }

    private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case Native.WM_PAINT:
                nint dc = Native.BeginPaint(hwnd, out var ps);
                Native.GetClientRect(hwnd, out var rect);
                Native.SetBkMode(dc, Native.TRANSPARENT);
                string text = "window " + Native.WindowText(hwnd)[^1];
                Native.DrawTextW(dc, text, -1, ref rect, Native.DT_SINGLELINE | Native.DT_VCENTER | 0x1 /* DT_CENTER */);
                Native.EndPaint(hwnd, ref ps);
                return 0;
            case Native.WM_TIMER:
            case Native.WM_DESTROY:
                Native.PostQuitMessage(0);
                return 0;
        }
        return Native.DefWindowProcW(hwnd, msg, wParam, lParam);
    }
}
