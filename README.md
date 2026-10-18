# Mohi AltSwitch

Press **Alt + `** to switch between the windows of the *same app* on Windows, the way
macOS (Cmd + `) and Linux (Alt + `) do.

> **Status:** early development. The behavior below is specified by a working
> AutoHotkey prototype (see [`prototype/`](prototype/)). A native C# rewrite and a
> Microsoft Store release are in progress; nothing is published to the Store yet.

## Behavior

| Input | What happens |
| --- | --- |
| `Alt` + `` ` `` (tap) | Instantly jump to the app's previous window |
| Hold `Alt`, keep tapping `` ` `` | After ~0.2 s a strip of live window previews appears; each tap moves the selection |
| `Alt` + `Shift` + `` ` `` | Move backwards through the list |
| Release `Alt` | Switch to the selected window (minimized windows are restored) |
| `Esc` while holding `Alt` | Cancel, stay where you are |

### What counts as "the same app"

Windows that belong to the same **executable**, not the same taskbar group.

This matters for Chrome and Edge: each browser profile gets its own taskbar identity
(for example `Chrome` vs `Chrome.UserData.Profile1`), so tools that group by that
identity can't reach windows of your other profiles. Mohi AltSwitch groups by
`chrome.exe`, so every profile's windows are in one list.

Only windows you could reach with Alt + Tab are listed: titled, not tool windows,
not owned dialogs, and on the current virtual desktop.

## Prototype

[`prototype/SameAppSwitcher-popup.ahk`](prototype/SameAppSwitcher-popup.ahk) is the
reference implementation (AutoHotkey v2). It is the executable specification the native
app has to match.

## Privacy

Mohi AltSwitch listens for the hotkey only. It does not record, store or transmit
keystrokes, window titles or anything else, and makes no network connections.

## License

No license has been chosen yet; until one is added, all rights are reserved.
