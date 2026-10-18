using System.Runtime.InteropServices;
using System.Text;

namespace MohiAltSwitch;

/// <summary>Every Win32 declaration the app uses, in one place.</summary>
internal static class Native
{
    public delegate nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam);
    public delegate nint HookProc(int code, nint wParam, nint lParam);
    public delegate bool EnumProc(nint hwnd, nint lParam);

    // ---- window messages -------------------------------------------------
    public const uint WM_NULL = 0x0000, WM_DESTROY = 0x0002, WM_PAINT = 0x000F, WM_ERASEBKGND = 0x0014,
        WM_MOUSEACTIVATE = 0x0021, WM_GETICON = 0x007F, WM_COMMAND = 0x0111, WM_TIMER = 0x0113,
        WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205, WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101,
        WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105, WM_APP = 0x8000;

    // ---- window styles ---------------------------------------------------
    public const uint WS_POPUP = 0x80000000, WS_VISIBLE = 0x10000000, WS_OVERLAPPEDWINDOW = 0x00CF0000;
    public const uint WS_EX_TOPMOST = 0x00000008, WS_EX_TOOLWINDOW = 0x00000080, WS_EX_APPWINDOW = 0x00040000,
        WS_EX_NOACTIVATE = 0x08000000;
    public const int GWL_EXSTYLE = -20;

    public const int SW_SHOWNOACTIVATE = 4, SW_RESTORE = 9;
    public const uint GW_OWNER = 4, GA_ROOTOWNER = 3;
    public const uint MONITOR_DEFAULTTONEAREST = 2;

    // ---- keyboard --------------------------------------------------------
    public const int WH_KEYBOARD_LL = 13, HC_ACTION = 0;
    public const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_ESCAPE = 0x1B, VK_LWIN = 0x5B, VK_RWIN = 0x5C;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    // ---- gdi -------------------------------------------------------------
    public const uint SRCCOPY = 0x00CC0020;
    public const int TRANSPARENT = 1, NULL_PEN = 8;
    public const uint DT_SINGLELINE = 0x20, DT_VCENTER = 0x04, DT_NOPREFIX = 0x800, DT_END_ELLIPSIS = 0x8000;
    public const uint DI_NORMAL = 0x0003;

    // ---- menus / tray ----------------------------------------------------
    public const uint MF_STRING = 0x0000, MF_CHECKED = 0x0008, MF_SEPARATOR = 0x0800, MF_POPUP = 0x0010;
    public const uint TPM_RIGHTBUTTON = 0x0002, TPM_BOTTOMALIGN = 0x0020, TPM_RIGHTALIGN = 0x0008, TPM_RETURNCMD = 0x0100;
    public const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
    public const uint NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4;

    // ---- structs ---------------------------------------------------------
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public nuint dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSEXW
    {
        public uint cbSize, style;
        public nint lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public nint hInstance, hIcon, hCursor, hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG { public nint hwnd; public uint message; public nint wParam, lParam; public uint time; public POINT pt; }

    [StructLayout(LayoutKind.Sequential)]
    public struct PAINTSTRUCT
    {
        public nint hdc; public int fErase; public RECT rcPaint; public int fRestore, fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[]? rgbReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO { public uint cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }

    [StructLayout(LayoutKind.Sequential)]
    public struct DWM_THUMBNAIL_PROPERTIES
    {
        public uint dwFlags; public RECT rcDestination, rcSource; public byte opacity; public int fVisible, fSourceClientAreaOnly;
    }
    public const uint DWM_TNP_RECTDESTINATION = 0x1, DWM_TNP_OPACITY = 0x4, DWM_TNP_VISIBLE = 0x8;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct NOTIFYICONDATAW
    {
        public uint cbSize; public nint hWnd; public uint uID, uFlags, uCallbackMessage; public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState, dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags; public Guid guidItem; public nint hBalloonIcon;
    }

    // ---- user32 ----------------------------------------------------------
    private const string User32 = "user32.dll", Gdi32 = "gdi32.dll", Dwm = "dwmapi.dll", Kernel32 = "kernel32.dll",
        Shell32 = "shell32.dll", Shcore = "shcore.dll";

    [DllImport(User32, ExactSpelling = true)] public static extern ushort RegisterClassExW(ref WNDCLASSEXW wc);
    [DllImport(User32, ExactSpelling = true, CharSet = CharSet.Unicode)]
    public static extern nint CreateWindowExW(uint exStyle, string className, string? title, uint style, int x, int y, int w, int h,
        nint parent, nint menu, nint instance, nint param);
    [DllImport(User32, ExactSpelling = true)] public static extern nint DefWindowProcW(nint hwnd, uint msg, nint wParam, nint lParam);
    [DllImport(User32, ExactSpelling = true)] public static extern bool DestroyWindow(nint hwnd);
    [DllImport(User32, ExactSpelling = true)] public static extern bool ShowWindow(nint hwnd, int cmd);
    [DllImport(User32, ExactSpelling = true)] public static extern bool UpdateWindow(nint hwnd);

    [DllImport(User32, ExactSpelling = true)] public static extern nint SetWindowsHookExW(int id, HookProc proc, nint module, uint threadId);
    [DllImport(User32, ExactSpelling = true)] public static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport(User32, ExactSpelling = true)] public static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [DllImport(User32, ExactSpelling = true)] public static extern int GetMessageW(out MSG msg, nint hwnd, uint min, uint max);
    [DllImport(User32, ExactSpelling = true)] public static extern bool TranslateMessage(ref MSG msg);
    [DllImport(User32, ExactSpelling = true)] public static extern nint DispatchMessageW(ref MSG msg);
    [DllImport(User32, ExactSpelling = true)] public static extern bool PostMessageW(nint hwnd, uint msg, nint wParam, nint lParam);
    [DllImport(User32, ExactSpelling = true)] public static extern void PostQuitMessage(int exitCode);
    [DllImport(User32, ExactSpelling = true)] public static extern nuint SetTimer(nint hwnd, nuint id, uint elapseMs, nint proc);
    [DllImport(User32, ExactSpelling = true)] public static extern bool KillTimer(nint hwnd, nuint id);
    [DllImport(User32, ExactSpelling = true)] public static extern uint RegisterWindowMessageW([MarshalAs(UnmanagedType.LPWStr)] string name);

    [DllImport(User32, ExactSpelling = true)] public static extern short GetAsyncKeyState(int vk);
    [DllImport(User32, ExactSpelling = true)] public static extern void keybd_event(byte vk, byte scan, uint flags, nuint extra);

    [DllImport(User32, ExactSpelling = true)] public static extern nint GetForegroundWindow();
    [DllImport(User32, ExactSpelling = true)] public static extern bool SetForegroundWindow(nint hwnd);
    [DllImport(User32, ExactSpelling = true)] public static extern bool BringWindowToTop(nint hwnd);
    [DllImport(User32, ExactSpelling = true)] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
    [DllImport(User32, ExactSpelling = true)] public static extern bool IsIconic(nint hwnd);
    [DllImport(User32, ExactSpelling = true)] public static extern bool IsWindow(nint hwnd);
    [DllImport(User32, ExactSpelling = true)] public static extern bool IsWindowVisible(nint hwnd);

    [DllImport(User32, ExactSpelling = true)] public static extern int GetWindowTextLengthW(nint hwnd);
    [DllImport(User32, ExactSpelling = true, CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(nint hwnd, StringBuilder text, int max);
    [DllImport(User32, ExactSpelling = true, CharSet = CharSet.Unicode)] public static extern int GetClassNameW(nint hwnd, StringBuilder text, int max);
    [DllImport(User32, ExactSpelling = true)] public static extern nint GetWindowLongPtrW(nint hwnd, int index);
    [DllImport(User32, ExactSpelling = true)] public static extern nint GetWindow(nint hwnd, uint cmd);
    [DllImport(User32, ExactSpelling = true)] public static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport(User32, ExactSpelling = true)] public static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport(User32, ExactSpelling = true)] public static extern bool EnumWindows(EnumProc proc, nint param);
    [DllImport(User32, ExactSpelling = true)] public static extern bool EnumChildWindows(nint hwnd, EnumProc proc, nint param);
    [DllImport(User32, ExactSpelling = true)] public static extern bool GetClientRect(nint hwnd, out RECT rect);
    [DllImport(User32, ExactSpelling = true)] public static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport(User32, ExactSpelling = true)] public static extern bool GetMonitorInfoW(nint monitor, ref MONITORINFO info);

    [DllImport(User32, ExactSpelling = true)]
    public static extern nint SendMessageTimeoutW(nint hwnd, uint msg, nuint wParam, nint lParam, uint flags, uint timeoutMs, out nuint result);
    [DllImport(User32, ExactSpelling = true)] public static extern nuint GetClassLongPtrW(nint hwnd, int index);
    [DllImport(User32, ExactSpelling = true)] public static extern nint LoadCursorW(nint instance, nint name);
    [DllImport(User32, ExactSpelling = true)] public static extern nint LoadIconW(nint instance, nint name);

    [DllImport(User32, ExactSpelling = true)] public static extern nint BeginPaint(nint hwnd, out PAINTSTRUCT ps);
    [DllImport(User32, ExactSpelling = true)] public static extern bool EndPaint(nint hwnd, ref PAINTSTRUCT ps);
    [DllImport(User32, ExactSpelling = true)] public static extern bool InvalidateRect(nint hwnd, nint rect, bool erase);
    [DllImport(User32, ExactSpelling = true)] public static extern int FillRect(nint hdc, ref RECT rect, nint brush);
    [DllImport(User32, ExactSpelling = true, CharSet = CharSet.Unicode)]
    public static extern int DrawTextW(nint hdc, string text, int count, ref RECT rect, uint format);
    [DllImport(User32, ExactSpelling = true)]
    public static extern bool DrawIconEx(nint hdc, int x, int y, nint icon, int cx, int cy, uint step, nint brush, uint flags);
    [DllImport(User32, ExactSpelling = true)] public static extern bool DestroyIcon(nint icon);

    [DllImport(User32, ExactSpelling = true)] public static extern nint CreatePopupMenu();
    [DllImport(User32, ExactSpelling = true, CharSet = CharSet.Unicode)] public static extern bool AppendMenuW(nint menu, uint flags, nuint id, string? text);
    [DllImport(User32, ExactSpelling = true)] public static extern int TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint hwnd, nint rect);
    [DllImport(User32, ExactSpelling = true)] public static extern bool DestroyMenu(nint menu);
    [DllImport(User32, ExactSpelling = true)] public static extern bool GetCursorPos(out POINT pt);

    // ---- gdi32 -----------------------------------------------------------
    [DllImport(Gdi32, ExactSpelling = true)] public static extern nint CreateCompatibleDC(nint hdc);
    [DllImport(Gdi32, ExactSpelling = true)] public static extern nint CreateCompatibleBitmap(nint hdc, int w, int h);
    [DllImport(Gdi32, ExactSpelling = true)] public static extern nint SelectObject(nint hdc, nint obj);
    [DllImport(Gdi32, ExactSpelling = true)] public static extern bool DeleteObject(nint obj);
    [DllImport(Gdi32, ExactSpelling = true)] public static extern bool DeleteDC(nint hdc);
    [DllImport(Gdi32, ExactSpelling = true)] public static extern bool BitBlt(nint dest, int x, int y, int w, int h, nint src, int sx, int sy, uint rop);
    [DllImport(Gdi32, ExactSpelling = true)] public static extern nint CreateSolidBrush(uint colorRef);
    [DllImport(Gdi32, ExactSpelling = true)] public static extern nint GetStockObject(int index);
    [DllImport(Gdi32, ExactSpelling = true)] public static extern bool RoundRect(nint hdc, int l, int t, int r, int b, int rw, int rh);
    [DllImport(Gdi32, ExactSpelling = true)] public static extern uint SetTextColor(nint hdc, uint colorRef);
    [DllImport(Gdi32, ExactSpelling = true)] public static extern int SetBkMode(nint hdc, int mode);
    [DllImport(Gdi32, ExactSpelling = true, CharSet = CharSet.Unicode)]
    public static extern nint CreateFontW(int height, int width, int escapement, int orientation, int weight, uint italic, uint underline,
        uint strikeOut, uint charSet, uint outPrecision, uint clipPrecision, uint quality, uint pitchAndFamily, string face);

    // ---- dwmapi ----------------------------------------------------------
    [DllImport(Dwm, ExactSpelling = true)] public static extern int DwmRegisterThumbnail(nint dest, nint src, out nint thumbnail);
    [DllImport(Dwm, ExactSpelling = true)] public static extern int DwmUnregisterThumbnail(nint thumbnail);
    [DllImport(Dwm, ExactSpelling = true)] public static extern int DwmUpdateThumbnailProperties(nint thumbnail, ref DWM_THUMBNAIL_PROPERTIES props);
    [DllImport(Dwm, ExactSpelling = true)] public static extern int DwmQueryThumbnailSourceSize(nint thumbnail, out SIZE size);
    [DllImport(Dwm, ExactSpelling = true)] public static extern int DwmGetWindowAttribute(nint hwnd, uint attr, out int value, uint size);
    [DllImport(Dwm, ExactSpelling = true)] public static extern int DwmSetWindowAttribute(nint hwnd, uint attr, ref int value, uint size);
    public const uint DWMWA_CLOAKED = 14, DWMWA_WINDOW_CORNER_PREFERENCE = 33;

    // ---- kernel32 / shell32 / shcore ---------------------------------------
    [DllImport(Kernel32, ExactSpelling = true, CharSet = CharSet.Unicode)] public static extern nint GetModuleHandleW(string? name);
    [DllImport(Kernel32, ExactSpelling = true)] public static extern nint OpenProcess(uint access, bool inherit, uint pid);
    [DllImport(Kernel32, ExactSpelling = true, CharSet = CharSet.Unicode)]
    public static extern bool QueryFullProcessImageNameW(nint process, uint flags, StringBuilder name, ref uint size);
    [DllImport(Kernel32, ExactSpelling = true)] public static extern bool CloseHandle(nint handle);
    [DllImport(Kernel32, ExactSpelling = true)] public static extern uint GetCurrentThreadId();
    [DllImport(Kernel32, ExactSpelling = true, CharSet = CharSet.Unicode)]
    public static extern int GetCurrentPackageFullName(ref uint length, StringBuilder? name);
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    public const int APPMODEL_ERROR_NO_PACKAGE = 15700;

    [DllImport(Shell32, ExactSpelling = true, CharSet = CharSet.Unicode)] public static extern bool Shell_NotifyIconW(uint message, ref NOTIFYICONDATAW data);
    [DllImport(Shell32, ExactSpelling = true, CharSet = CharSet.Unicode)]
    public static extern nint ShellExecuteW(nint hwnd, string? verb, string file, string? args, string? dir, int show);
    [DllImport(Shell32, ExactSpelling = true, CharSet = CharSet.Unicode)]
    public static extern uint ExtractIconExW(string file, int index, nint large, out nint small, uint count);

    [DllImport(Shcore, ExactSpelling = true)] public static extern int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);

    // ---- small helpers ---------------------------------------------------
    public static string WindowText(nint hwnd)
    {
        int len = GetWindowTextLengthW(hwnd);
        if (len <= 0) return "";
        var sb = new StringBuilder(len + 1);
        GetWindowTextW(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string ClassName(nint hwnd)
    {
        var sb = new StringBuilder(256);
        GetClassNameW(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static bool KeyDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;
}
