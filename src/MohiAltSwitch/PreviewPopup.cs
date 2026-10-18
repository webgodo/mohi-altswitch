using System.Runtime.InteropServices;

namespace MohiAltSwitch;

/// <summary>
/// The strip of live window previews. A borderless, topmost window that never takes focus. Tiles and titles are
/// drawn with GDI; the live thumbnails are Windows' own DWM thumbnails composited on top.
/// </summary>
internal sealed class PreviewPopup
{
    public const string WindowTitle = "MohiAltSwitchPopup";
    private const string ClassName = "MohiAltSwitch.Popup";

    // Layout at 100% DPI; everything is multiplied by the monitor's scale.
    private const int CellW = 250, CellH = 180, Margin = 6, Pad = 8, TitleH = 20, IconSize = 16, Radius = 8, Ring = 3;

    // COLORREF is 0x00BBGGRR.
    private const uint BackColor = 0x1C1C1C, TileColor = 0x2B2B2B, AccentColor = 0xD47800, TextColor = 0xFFFFFF;

    private static readonly Native.WndProc s_wndProc = WndProc;
    private static PreviewPopup? s_current;
    private static bool s_classRegistered;

    private sealed record Entry(nint Window, string Title, nint Icon);

    private readonly List<nint> _thumbnails = new();
    private List<Entry> _entries = new();
    private nint _hwnd, _font;
    private int _selected, _columns, _cellW, _cellH, _margin, _pad, _titleH, _icon, _radius, _ring;

    public bool Visible => _hwnd != 0;

    public void Show(IReadOnlyList<nint> windows, int selected)
    {
        Hide();
        EnsureClass();

        nint monitor = Native.MonitorFromWindow(windows[0], Native.MONITOR_DEFAULTTONEAREST);
        var info = new Native.MONITORINFO { cbSize = (uint)Marshal.SizeOf<Native.MONITORINFO>() };
        Native.GetMonitorInfoW(monitor, ref info);
        uint dpi = Native.GetDpiForMonitor(monitor, 0, out uint dpiX, out _) == 0 ? dpiX : 96;
        double scale = dpi / 96.0;

        _cellW = (int)Math.Round(CellW * scale); _cellH = (int)Math.Round(CellH * scale);
        _margin = (int)Math.Round(Margin * scale); _pad = (int)Math.Round(Pad * scale);
        _titleH = (int)Math.Round(TitleH * scale); _icon = (int)Math.Round(IconSize * scale);
        _radius = (int)Math.Round(Radius * scale); _ring = (int)Math.Round(Ring * scale);

        var work = info.rcWork;
        _columns = Math.Max(1, Math.Min(windows.Count, (int)(work.Width * 0.9 / _cellW)));
        int rows = (windows.Count + _columns - 1) / _columns;
        int width = _columns * _cellW, height = rows * _cellH;

        _entries = windows.Select(w => new Entry(w, Native.WindowText(w), IconOf(w))).ToList();
        _selected = selected;
        _font = Native.CreateFontW(-(int)Math.Round(9 * dpi / 72.0), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");

        s_current = this;
        _hwnd = Native.CreateWindowExW(
            Native.WS_EX_TOPMOST | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE, ClassName, WindowTitle, Native.WS_POPUP,
            work.Left + (work.Width - width) / 2, work.Top + (work.Height - height) / 2, width, height,
            0, 0, Native.GetModuleHandleW(null), 0);
        if (_hwnd == 0)
        {
            Hide();
            return;
        }

        int rounded = 2;                                           // DWMWCP_ROUND: rounded corners on Windows 11
        Native.DwmSetWindowAttribute(_hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref rounded, sizeof(int));

        for (int i = 0; i < _entries.Count; i++)
            AddThumbnail(_entries[i].Window, TileRect(i));

        Native.ShowWindow(_hwnd, Native.SW_SHOWNOACTIVATE);
        Native.UpdateWindow(_hwnd);
    }

    public void Select(int index)
    {
        _selected = index;
        if (_hwnd != 0)
            Native.InvalidateRect(_hwnd, 0, false);
    }

    public void Hide()
    {
        foreach (nint thumb in _thumbnails)
            Native.DwmUnregisterThumbnail(thumb);
        _thumbnails.Clear();

        if (_hwnd != 0)
        {
            Native.DestroyWindow(_hwnd);
            _hwnd = 0;
        }
        if (_font != 0)
        {
            Native.DeleteObject(_font);
            _font = 0;
        }
        if (s_current == this)
            s_current = null;
    }

    // ---- layout ------------------------------------------------------------

    private Native.RECT TileRect(int i)
    {
        int x = i % _columns * _cellW + _margin, y = i / _columns * _cellH + _margin;
        return new Native.RECT { Left = x, Top = y, Right = x + _cellW - 2 * _margin, Bottom = y + _cellH - 2 * _margin };
    }

    /// <summary>The area inside a tile that holds the live preview (everything above the title row).</summary>
    private Native.RECT PreviewBox(Native.RECT tile) => new()
    {
        Left = tile.Left + _pad, Top = tile.Top + _pad, Right = tile.Right - _pad, Bottom = tile.Bottom - 2 * _pad - _titleH,
    };

    // ---- live thumbnails -------------------------------------------------

    /// <summary>Registers a live DWM preview of <paramref name="source"/>, scaled to fit (never enlarged) and centred.</summary>
    private void AddThumbnail(nint source, Native.RECT tile)
    {
        if (Native.DwmRegisterThumbnail(_hwnd, source, out nint thumb) != 0)
            return;
        if (Native.DwmQueryThumbnailSourceSize(thumb, out var size) != 0 || size.cx < 1 || size.cy < 1)
        {
            Native.DwmUnregisterThumbnail(thumb);
            return;
        }

        var box = PreviewBox(tile);
        double k = Math.Min(1.0, Math.Min((double)box.Width / size.cx, (double)box.Height / size.cy));
        int w = (int)Math.Round(size.cx * k), h = (int)Math.Round(size.cy * k);
        int left = box.Left + (box.Width - w) / 2, top = box.Top + (box.Height - h) / 2;

        var props = new Native.DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = Native.DWM_TNP_RECTDESTINATION | Native.DWM_TNP_OPACITY | Native.DWM_TNP_VISIBLE,
            rcDestination = new Native.RECT { Left = left, Top = top, Right = left + w, Bottom = top + h },
            opacity = 255,
            fVisible = 1,
        };
        Native.DwmUpdateThumbnailProperties(thumb, ref props);
        _thumbnails.Add(thumb);
    }

    // ---- painting --------------------------------------------------------

    private void Paint()
    {
        nint hdc = Native.BeginPaint(_hwnd, out var ps);
        try
        {
            Native.GetClientRect(_hwnd, out var client);
            nint mem = Native.CreateCompatibleDC(hdc);
            nint bitmap = Native.CreateCompatibleBitmap(hdc, client.Right, client.Bottom);
            nint oldBitmap = Native.SelectObject(mem, bitmap);

            Fill(mem, client, BackColor);
            DrawTiles(mem);

            Native.BitBlt(hdc, 0, 0, client.Right, client.Bottom, mem, 0, 0, Native.SRCCOPY);
            Native.SelectObject(mem, oldBitmap);
            Native.DeleteObject(bitmap);
            Native.DeleteDC(mem);
        }
        finally
        {
            Native.EndPaint(_hwnd, ref ps);
        }
    }

    private void DrawTiles(nint dc)
    {
        nint tileBrush = Native.CreateSolidBrush(TileColor), ringBrush = Native.CreateSolidBrush(AccentColor);
        nint oldPen = Native.SelectObject(dc, Native.GetStockObject(Native.NULL_PEN));
        nint oldFont = Native.SelectObject(dc, _font);
        Native.SetBkMode(dc, Native.TRANSPARENT);
        Native.SetTextColor(dc, TextColor);

        for (int i = 0; i < _entries.Count; i++)
        {
            var tile = TileRect(i);
            if (i == _selected)
            {
                nint old = Native.SelectObject(dc, ringBrush);
                Native.RoundRect(dc, tile.Left - _ring, tile.Top - _ring, tile.Right + _ring + 1, tile.Bottom + _ring + 1,
                    2 * (_radius + _ring), 2 * (_radius + _ring));
                Native.SelectObject(dc, old);
            }
            nint previous = Native.SelectObject(dc, tileBrush);
            Native.RoundRect(dc, tile.Left, tile.Top, tile.Right + 1, tile.Bottom + 1, 2 * _radius, 2 * _radius);
            Native.SelectObject(dc, previous);

            int rowTop = tile.Bottom - _pad - _titleH;
            if (_entries[i].Icon != 0)
                Native.DrawIconEx(dc, tile.Left + _pad, rowTop + (_titleH - _icon) / 2, _entries[i].Icon, _icon, _icon, 0, 0, Native.DI_NORMAL);

            var text = new Native.RECT
            {
                Left = tile.Left + _pad + _icon + _pad / 2, Top = rowTop, Right = tile.Right - _pad, Bottom = rowTop + _titleH,
            };
            Native.DrawTextW(dc, _entries[i].Title, -1, ref text,
                Native.DT_SINGLELINE | Native.DT_VCENTER | Native.DT_NOPREFIX | Native.DT_END_ELLIPSIS);
        }

        Native.SelectObject(dc, oldFont);
        Native.SelectObject(dc, oldPen);
        Native.DeleteObject(tileBrush);
        Native.DeleteObject(ringBrush);
    }

    private static void Fill(nint dc, Native.RECT rect, uint color)
    {
        nint brush = Native.CreateSolidBrush(color);
        Native.FillRect(dc, ref rect, brush);
        Native.DeleteObject(brush);
    }

    /// <summary>The window's small icon, asking politely (with a timeout, so a hung window can't stall the popup).</summary>
    private static nint IconOf(nint hwnd)
    {
        foreach (nuint kind in new nuint[] { 2 /* ICON_SMALL2 */, 0 /* ICON_SMALL */, 1 /* ICON_BIG */ })
        {
            Native.SendMessageTimeoutW(hwnd, Native.WM_GETICON, kind, 0, 0x0002 /* SMTO_ABORTIFHUNG */, 100, out nuint icon);
            if (icon != 0)
                return (nint)icon;
        }
        nuint fromClass = Native.GetClassLongPtrW(hwnd, -34 /* GCLP_HICONSM */);
        if (fromClass == 0)
            fromClass = Native.GetClassLongPtrW(hwnd, -14 /* GCLP_HICON */);
        return fromClass != 0 ? (nint)fromClass : Native.LoadIconW(0, 32512 /* IDI_APPLICATION */);
    }

    // ---- window class ----------------------------------------------------

    private static void EnsureClass()
    {
        if (s_classRegistered)
            return;
        var wc = new Native.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<Native.WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProc),
            hInstance = Native.GetModuleHandleW(null),
            hCursor = Native.LoadCursorW(0, 32512 /* IDC_ARROW */),
            lpszClassName = ClassName,
        };
        Native.RegisterClassExW(ref wc);
        s_classRegistered = true;
    }

    private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        try
        {
            switch (msg)
            {
                case Native.WM_PAINT when s_current is { } popup && popup._hwnd == hwnd:
                    popup.Paint();
                    return 0;
                case Native.WM_ERASEBKGND:
                    return 1;                                  // we paint every pixel ourselves
                case Native.WM_MOUSEACTIVATE:
                    return 3;                                  // MA_NOACTIVATE: clicking the popup never steals focus
            }
        }
        catch
        {
            // never let an exception escape a window procedure
        }
        return Native.DefWindowProcW(hwnd, msg, wParam, lParam);
    }
}
