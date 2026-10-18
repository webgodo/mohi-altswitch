using System.Runtime.InteropServices;

namespace MohiAltSwitch;

/// <summary>
/// Low-level keyboard hook that reacts to exactly two things: Alt+` (Alt+Shift+` for backwards) and, while a
/// switch is in progress, Esc. Every other key is passed straight through untouched. Nothing is recorded,
/// stored or sent anywhere.
/// </summary>
internal sealed unsafe class KeyboardHook : IDisposable
{
    private const uint ScanGrave = 0x29;               // the ` / ~ key, whatever the keyboard layout
    private const uint LlkhfExtended = 0x01;

    private readonly Native.HookProc _proc;            // keep the delegate alive for as long as the hook exists
    private readonly nint _target;
    private readonly uint _stepMessage, _cancelMessage;
    private readonly Func<bool> _switchInProgress;
    private nint _hook;
    private bool _swallowGraveUp, _swallowEscUp;

    public KeyboardHook(nint targetWindow, uint stepMessage, uint cancelMessage, Func<bool> switchInProgress)
    {
        _target = targetWindow;
        _stepMessage = stepMessage;
        _cancelMessage = cancelMessage;
        _switchInProgress = switchInProgress;
        _proc = Callback;
    }

    public bool Install()
    {
        _hook = Native.SetWindowsHookExW(Native.WH_KEYBOARD_LL, _proc, Native.GetModuleHandleW(null), 0);
        return _hook != 0;
    }

    private nint Callback(int code, nint wParam, nint lParam)
    {
        try
        {
            if (code == Native.HC_ACTION && Handle((uint)wParam, (Native.KBDLLHOOKSTRUCT*)lParam))
                return 1;                              // swallow the key
        }
        catch
        {
            // a hook must never throw into the OS; fall through and let the key pass
        }
        return Native.CallNextHookEx(_hook, code, wParam, lParam);
    }

    private bool Handle(uint message, Native.KBDLLHOOKSTRUCT* key)
    {
        bool down = message is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN;
        bool up = message is Native.WM_KEYUP or Native.WM_SYSKEYUP;

        if (key->scanCode == ScanGrave && (key->flags & LlkhfExtended) == 0)
        {
            if (down && Native.KeyDown(Native.VK_MENU) && !Native.KeyDown(Native.VK_CONTROL) &&
                !Native.KeyDown(Native.VK_LWIN) && !Native.KeyDown(Native.VK_RWIN))
            {
                _swallowGraveUp = true;
                int direction = Native.KeyDown(Native.VK_SHIFT) ? -1 : 1;
                Native.PostMessageW(_target, _stepMessage, direction, 0);   // do the real work outside the hook
                return true;
            }
            if (up && _swallowGraveUp)
            {
                _swallowGraveUp = false;
                return true;
            }
        }
        else if (key->vkCode == Native.VK_ESCAPE)
        {
            if (down && _switchInProgress())
            {
                _swallowEscUp = true;
                Native.PostMessageW(_target, _cancelMessage, 0, 0);
                return true;
            }
            if (up && _swallowEscUp)
            {
                _swallowEscUp = false;
                return true;
            }
        }
        return false;
    }

    public void Dispose()
    {
        if (_hook != 0)
        {
            Native.UnhookWindowsHookEx(_hook);
            _hook = 0;
        }
    }
}
