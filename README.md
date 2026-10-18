# Mohi AltSwitch

Press **Alt + `** to switch between the windows of the *same app* on Windows, the way
macOS (Cmd + `) and Linux (Alt + `) do, with a live preview strip.

> **Status:** early development. The native C# app works from source and as a per-user test
> installer; a Microsoft Store package is the next step. Nothing is published to the Store yet.

## Behavior

| Input | What happens |
| --- | --- |
| `Alt` + `` ` `` (tap) | Instantly jump to the app's previous window |
| Hold `Alt`, keep tapping `` ` `` | After ~0.2 s a strip of live window previews appears; each tap moves the selection |
| `Alt` + `Shift` + `` ` `` | Move backwards through the list |
| Release `Alt` | Switch to the selected window (minimized windows are restored) |
| `Esc` while holding `Alt` | Cancel, stay where you are |

The tray icon's menu turns the preview on or off, sets the preview delay, and controls
start-with-Windows.

### What counts as "the same app"

Windows that belong to the same **executable**, not the same taskbar group.

This matters for Chrome and Edge: each browser profile gets its own taskbar identity
(for example `Chrome` vs `Chrome.UserData.Profile1`), so tools that group by that
identity can't reach windows of your other profiles. Mohi AltSwitch groups by
`chrome.exe`, so every profile's windows are in one list.

Only windows you could reach with Alt + Tab are listed: titled, not tool windows,
not owned dialogs, and on the current virtual desktop. Windows running as administrator
can't be switched from a normal app (a Windows security rule).

## Install a test build

Build the installer package (see below), then double-click `Install.cmd` in the folder it
creates. It installs for the current user only (no administrator rights), adds a Start menu
shortcut and a Settings > Apps entry, and starts the app at sign-in. This test build needs the
.NET 10 runtime (`winget install Microsoft.DotNet.Runtime.10`).

If another tool already owns Alt + ` (AltExten, an AutoHotkey script, ...), set that tool's window
hotkey to something else: whichever keyboard hook was installed last wins.

## Build from source

Requirements: Windows 10/11, the .NET 10 SDK (`winget install Microsoft.DotNet.SDK.10`).

```powershell
tools\build.ps1        # build + publish to %LOCALAPPDATA%\MohiAltSwitchBuild\out
tools\package.ps1      # build + pack the per-user installer into Documents\MohiAltSwitch
```

The scripts mirror the sources to a Windows-local folder first, because Windows build tools
handle `\\wsl.localhost\...` paths badly (this repo is developed from WSL).

## Tests

`tests/integration/Run-IntegrationTests.ps1` starts the built app, opens three throwaway windows
and drives them with real injected keystrokes (tap, hold, backwards, cancel, minimized window),
saving screenshots of the popup. It drives a live desktop, so it only sends keys while one of its
own test windows has focus, and it needs an unlocked screen.

```powershell
tests\integration\Run-IntegrationTests.ps1 `
  -AppExe        "$env:LOCALAPPDATA\MohiAltSwitchBuild\out\app\MohiAltSwitch.exe" `
  -TestWindowsExe "$env:LOCALAPPDATA\MohiAltSwitchBuild\out\testwindows\TestWindows.exe"
```

## Layout

| Path | What |
| --- | --- |
| `src/MohiAltSwitch` | The app (C#, plain Win32 via P/Invoke, no UI framework) |
| `tests/TestWindows` | Helper that opens the throwaway windows for the integration tests |
| `tests/integration` | End-to-end keystroke tests |
| `tools` | Build, package and icon-generation scripts, and the installer scripts |
| `prototype` | The AutoHotkey v2 prototype this behavior was specified with |

## Privacy

Mohi AltSwitch listens for the hotkey only. It does not record, store or transmit
keystrokes, window titles or anything else, and makes no network connections.

## License

No license has been chosen yet; until one is added, all rights are reserved.
