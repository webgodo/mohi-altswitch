namespace MohiAltSwitch;

/// <summary>
/// One Alt-hold = one switch. The first Alt+` snapshots the app's windows; every further press moves the
/// selection; releasing Alt activates the selected window (Esc cancels). The preview popup appears only if Alt
/// is still held after a short delay, so a quick tap switches instantly with no flash.
/// </summary>
internal sealed class Switcher
{
    public const nuint TimerId = 1;
    private const uint PollMs = 20;
    private const byte UnassignedKey = 0xE8;                  // the "mask" key (see MaskAltRelease)

    private readonly nint _owner;
    private readonly Settings _settings;
    private readonly PreviewPopup _popup = new();
    private List<nint> _windows = new();
    private int _selected;
    private long _showAt;

    public Switcher(nint owner, Settings settings)
    {
        _owner = owner;
        _settings = settings;
    }

    public bool InProgress { get; private set; }

    /// <summary>Alt+` (direction 1) or Alt+Shift+` (direction -1) was pressed.</summary>
    public void Step(int direction)
    {
        if (!InProgress)
        {
            nint current = Native.GetForegroundWindow();
            if (current == 0)
                return;
            var windows = WindowCatalog.AppWindows(current);
            if (windows.Count < 2)
                return;                                       // only one window: nothing to switch to

            _windows = windows;
            _selected = 0;                                    // 0 is where we started
            _showAt = Environment.TickCount64 + _settings.PreviewDelayMs;
            InProgress = true;
            Native.SetTimer(_owner, TimerId, PollMs, 0);
            MaskAltRelease();
        }

        _selected = (_selected + direction + _windows.Count) % _windows.Count;
        if (_popup.Visible)
            _popup.Select(_selected);
        else
            ShowPopupIfDue();
    }

    /// <summary>Called every 20 ms while a switch is in progress.</summary>
    public void Tick()
    {
        if (!Native.KeyDown(Native.VK_MENU))
            Finish(cancel: false);
        else if (!_popup.Visible)
            ShowPopupIfDue();
    }

    public void Cancel() => Finish(cancel: true);

    private void ShowPopupIfDue()
    {
        if (_settings.ShowPreview && Environment.TickCount64 >= _showAt)
            _popup.Show(_windows, _selected);
    }

    private void Finish(bool cancel)
    {
        if (!InProgress)
            return;
        Native.KillTimer(_owner, TimerId);
        _popup.Hide();
        InProgress = false;

        nint target = _windows[_selected];
        if (cancel || _selected == 0 || !Native.IsWindow(target))
            return;
        Activate(target);
    }

    /// <summary>
    /// Because the hook swallows the ` key, apps would see Alt pressed and released on its own, which opens the
    /// menu bar in many of them. A tap of an unassigned key in between makes it a normal key combination instead.
    /// </summary>
    private static void MaskAltRelease()
    {
        Native.keybd_event(UnassignedKey, 0, 0, 0);
        Native.keybd_event(UnassignedKey, 0, Native.KEYEVENTF_KEYUP, 0);
    }

    private static void Activate(nint hwnd)
    {
        if (Native.IsIconic(hwnd))
            Native.ShowWindow(hwnd, Native.SW_RESTORE);

        Native.SetForegroundWindow(hwnd);
        if (Native.GetForegroundWindow() == hwnd)
            return;

        // The foreground lock refused: borrow the input queue of whichever window is in front, then try again.
        uint me = Native.GetCurrentThreadId();
        nint front = Native.GetForegroundWindow();
        uint frontThread = front != 0 ? Native.GetWindowThreadProcessId(front, out _) : 0;
        bool attached = frontThread != 0 && frontThread != me && Native.AttachThreadInput(me, frontThread, true);
        Native.BringWindowToTop(hwnd);
        Native.SetForegroundWindow(hwnd);
        if (attached)
            Native.AttachThreadInput(me, frontThread, false);
        if (Native.GetForegroundWindow() == hwnd)
            return;

        // Last resort: a tap of Alt counts as user input and lifts the lock.
        Native.keybd_event(Native.VK_MENU, 0, 0, 0);
        Native.keybd_event(Native.VK_MENU, 0, Native.KEYEVENTF_KEYUP, 0);
        Native.SetForegroundWindow(hwnd);
    }
}
