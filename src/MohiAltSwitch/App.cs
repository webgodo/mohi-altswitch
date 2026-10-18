using System.Runtime.InteropServices;

namespace MohiAltSwitch;

/// <summary>Owns the hidden window, the keyboard hook and the tray icon, and runs the message loop.</summary>
internal sealed class App
{
    public const string ProjectUrl = "https://github.com/webgodo/mohi-altswitch";

    private const string WindowClass = "MohiAltSwitch.Main";
    private const uint WmStep = Native.WM_APP + 1, WmCancel = Native.WM_APP + 2, WmTray = Native.WM_APP + 3;

    // Tray menu command ids
    private const int CmdPreview = 101, CmdDelayBase = 110, CmdStartup = 120, CmdStartupSettings = 121, CmdAbout = 130, CmdExit = 199;
    private static readonly int[] DelayChoicesMs = { 0, 100, 200, 400 };
    private static readonly string[] DelayLabels = { "Instant", "Short (0.1 s)", "Normal (0.2 s)", "Long (0.4 s)" };

    private static readonly Native.WndProc s_wndProc = WndProc;
    private static App? s_instance;

    private readonly Settings _settings = Settings.Load();
    private nint _hwnd;
    private uint _taskbarCreated;
    private Switcher? _switcher;
    private KeyboardHook? _hook;
    private TrayIcon? _tray;

    public int Run()
    {
        s_instance = this;
        var wc = new Native.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<Native.WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProc),
            hInstance = Native.GetModuleHandleW(null),
            lpszClassName = WindowClass,
        };
        if (Native.RegisterClassExW(ref wc) == 0)
            return 1;

        // A normal (never shown) top-level window, so we also receive the "TaskbarCreated" broadcast.
        _hwnd = Native.CreateWindowExW(Native.WS_EX_TOOLWINDOW, WindowClass, "Mohi AltSwitch", Native.WS_POPUP,
            0, 0, 0, 0, 0, 0, wc.hInstance, 0);
        if (_hwnd == 0)
            return 1;

        _taskbarCreated = Native.RegisterWindowMessageW("TaskbarCreated");
        _switcher = new Switcher(_hwnd, _settings);
        _hook = new KeyboardHook(_hwnd, WmStep, WmCancel, () => _switcher.InProgress);
        if (!_hook.Install())
            return 2;
        _tray = new TrayIcon(_hwnd, WmTray, "Mohi AltSwitch  (Alt + `)");
        _tray.Add();

        while (Native.GetMessageW(out var msg, 0, 0, 0) > 0)
        {
            Native.TranslateMessage(ref msg);
            Native.DispatchMessageW(ref msg);
        }

        _tray.Dispose();
        _hook.Dispose();
        return 0;
    }

    private nint HandleMessage(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case WmStep:
                _switcher!.Step((int)wParam);
                return 0;
            case WmCancel:
                _switcher!.Cancel();
                return 0;
            case Native.WM_TIMER when (nuint)wParam == Switcher.TimerId:
                _switcher!.Tick();
                return 0;
            case WmTray:
                if ((uint)lParam is Native.WM_RBUTTONUP or Native.WM_LBUTTONUP)
                    ShowMenu();
                return 0;
            case Native.WM_DESTROY:
                Native.PostQuitMessage(0);
                return 0;
        }
        if (_taskbarCreated != 0 && msg == _taskbarCreated)
        {
            _tray?.Add();                                      // Explorer restarted: put the icon back
            return 0;
        }
        return Native.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private void ShowMenu()
    {
        bool packaged = Startup.IsPackaged;

        nint delayMenu = Native.CreatePopupMenu();
        for (int i = 0; i < DelayChoicesMs.Length; i++)
            Native.AppendMenuW(delayMenu, Native.MF_STRING | (_settings.PreviewDelayMs == DelayChoicesMs[i] ? Native.MF_CHECKED : 0),
                (nuint)(CmdDelayBase + i), DelayLabels[i]);

        nint menu = Native.CreatePopupMenu();
        Native.AppendMenuW(menu, Native.MF_STRING | (_settings.ShowPreview ? Native.MF_CHECKED : 0), CmdPreview, "Show preview popup");
        Native.AppendMenuW(menu, Native.MF_POPUP, (nuint)delayMenu, "Preview delay");
        Native.AppendMenuW(menu, Native.MF_SEPARATOR, 0, null);
        if (packaged)
            Native.AppendMenuW(menu, Native.MF_STRING, CmdStartupSettings, "Start with Windows (opens Settings)...");
        else
            Native.AppendMenuW(menu, Native.MF_STRING | (Startup.Enabled ? Native.MF_CHECKED : 0), CmdStartup, "Start with Windows");
        Native.AppendMenuW(menu, Native.MF_STRING, CmdAbout, "About Mohi AltSwitch...");
        Native.AppendMenuW(menu, Native.MF_SEPARATOR, 0, null);
        Native.AppendMenuW(menu, Native.MF_STRING, CmdExit, "Exit");

        Native.GetCursorPos(out var pt);
        Native.SetForegroundWindow(_hwnd);                     // required, or the menu never closes when you click away
        int command = Native.TrackPopupMenu(menu, Native.TPM_RETURNCMD | Native.TPM_RIGHTBUTTON | Native.TPM_BOTTOMALIGN | Native.TPM_RIGHTALIGN,
            pt.X, pt.Y, 0, _hwnd, 0);
        Native.PostMessageW(_hwnd, Native.WM_NULL, 0, 0);
        Native.DestroyMenu(menu);                              // also destroys the submenu

        OnCommand(command);
    }

    private void OnCommand(int command)
    {
        switch (command)
        {
            case CmdPreview:
                _settings.ShowPreview = !_settings.ShowPreview;
                _settings.Save();
                break;
            case >= CmdDelayBase and < CmdDelayBase + 4:
                _settings.PreviewDelayMs = DelayChoicesMs[command - CmdDelayBase];
                _settings.Save();
                break;
            case CmdStartup:
                Startup.Set(!Startup.Enabled);
                break;
            case CmdStartupSettings:
                Native.ShellExecuteW(0, "open", "ms-settings:startupapps", null, null, 1);
                break;
            case CmdAbout:
                Native.ShellExecuteW(0, "open", ProjectUrl, null, null, 1);
                break;
            case CmdExit:
                Native.DestroyWindow(_hwnd);
                break;
        }
    }

    private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        try
        {
            if (s_instance is { } app)
                return app.HandleMessage(hwnd, msg, wParam, lParam);
        }
        catch
        {
            // never let an exception escape a window procedure
        }
        return Native.DefWindowProcW(hwnd, msg, wParam, lParam);
    }
}
