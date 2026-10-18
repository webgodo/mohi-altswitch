using System.Text;

namespace MohiAltSwitch;

/// <summary>Finds the windows that belong to the same app as a given window.</summary>
internal static class WindowCatalog
{
    /// <summary>
    /// The window you start from first, then the app's other windows in z-order (most recently used first).
    /// "Same app" means same executable, not same taskbar group: Chrome gives every profile its own
    /// taskbar identity, but every profile is still chrome.exe, so all of them are included.
    /// </summary>
    public static List<nint> AppWindows(nint current)
    {
        var result = new List<nint> { current };
        string app = AppId(current);
        if (app.Length == 0)
            return result;                                   // can't identify the app (e.g. elevated): nothing to cycle

        nint rootOwner = Native.GetAncestor(current, Native.GA_ROOTOWNER);   // a dialog's owner is the same logical window
        Native.EnumWindows((hwnd, _) =>                      // EnumWindows walks top to bottom in z-order
        {
            if (hwnd != current && hwnd != rootOwner && IsSwitchable(hwnd) &&
                string.Equals(AppId(hwnd), app, StringComparison.OrdinalIgnoreCase))
                result.Add(hwnd);
            return true;
        }, 0);
        return result;
    }

    /// <summary>Executable path of the window's app, or "" if it can't be determined.</summary>
    public static string AppId(nint hwnd)
    {
        string path = ProcessPath(hwnd);
        if (!path.EndsWith("\\ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
            return path;

        // Store (UWP) apps all share ApplicationFrameHost.exe; the real app owns the CoreWindow inside the frame.
        string inner = "";
        Native.EnumChildWindows(hwnd, (child, _) =>
        {
            if (Native.ClassName(child) != "Windows.UI.Core.CoreWindow")
                return true;
            inner = ProcessPath(child);
            return false;
        }, 0);
        return inner.Length > 0 ? inner : path;
    }

    /// <summary>
    /// Roughly the test Alt+Tab applies: titled, visible, not a tool window, not an owned dialog, and not cloaked
    /// (cloaked = on another virtual desktop, or a suspended Store app).
    /// </summary>
    public static bool IsSwitchable(nint hwnd)
    {
        if (!Native.IsWindowVisible(hwnd))
            return false;

        long ex = Native.GetWindowLongPtrW(hwnd, Native.GWL_EXSTYLE);
        if ((ex & Native.WS_EX_NOACTIVATE) != 0)             // overlays and popups
            return false;
        bool appWindow = (ex & Native.WS_EX_APPWINDOW) != 0;
        if ((ex & Native.WS_EX_TOOLWINDOW) != 0 && !appWindow)
            return false;
        if (!appWindow && Native.GetWindow(hwnd, Native.GW_OWNER) != 0)
            return false;

        if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            return false;

        if (Native.GetWindowTextLengthW(hwnd) == 0)
            return false;

        return Native.ClassName(hwnd) is not
            ("Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "XamlExplorerHostIslandWindow");
    }

    private static string ProcessPath(nint hwnd)
    {
        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0)
            return "";
        nint process = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == 0)
            return "";
        try
        {
            var sb = new StringBuilder(1024);
            uint size = (uint)sb.Capacity;
            return Native.QueryFullProcessImageNameW(process, 0, sb, ref size) ? sb.ToString() : "";
        }
        finally
        {
            Native.CloseHandle(process);
        }
    }
}
