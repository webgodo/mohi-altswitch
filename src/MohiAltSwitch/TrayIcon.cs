using System.Runtime.InteropServices;

namespace MohiAltSwitch;

/// <summary>The notification-area icon. Mouse events arrive at the owner window as <c>callbackMessage</c>.</summary>
internal sealed class TrayIcon : IDisposable
{
    private const uint IconId = 1;

    private readonly nint _owner;
    private readonly uint _callbackMessage;
    private readonly string _tip;
    private nint _icon;
    private bool _added;

    public TrayIcon(nint owner, uint callbackMessage, string tip)
    {
        _owner = owner;
        _callbackMessage = callbackMessage;
        _tip = tip;
    }

    /// <summary>Adds the icon, or re-adds it after Explorer restarted.</summary>
    public void Add()
    {
        if (_icon == 0)
            _icon = LoadAppIcon();
        var data = Data(Native.NIF_MESSAGE | Native.NIF_ICON | Native.NIF_TIP);
        _added = Native.Shell_NotifyIconW(Native.NIM_ADD, ref data);
    }

    public void Dispose()
    {
        if (_added)
        {
            var data = Data(0);
            Native.Shell_NotifyIconW(Native.NIM_DELETE, ref data);
            _added = false;
        }
        if (_icon != 0)
        {
            Native.DestroyIcon(_icon);
            _icon = 0;
        }
    }

    private Native.NOTIFYICONDATAW Data(uint flags) => new()
    {
        cbSize = (uint)Marshal.SizeOf<Native.NOTIFYICONDATAW>(),
        hWnd = _owner,
        uID = IconId,
        uFlags = flags,
        uCallbackMessage = _callbackMessage,
        hIcon = _icon,
        szTip = _tip,
        szInfo = "",
        szInfoTitle = "",
    };

    /// <summary>The icon embedded in our own .exe, or the generic application icon if that fails.</summary>
    private static nint LoadAppIcon()
    {
        if (Environment.ProcessPath is { } exe && Native.ExtractIconExW(exe, 0, 0, out nint small, 1) > 0 && small != 0)
            return small;
        return Native.LoadIconW(0, 32512 /* IDI_APPLICATION */);
    }
}
