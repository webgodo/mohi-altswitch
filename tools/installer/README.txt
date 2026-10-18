Mohi AltSwitch - local test build
================================

Alt + `            switch to the app's previous window
Hold Alt, tap `    walk through the app's windows; a preview strip appears after a moment
Alt + Shift + `    walk backwards
Esc (Alt held)     cancel
Release Alt        switch to the selected window

Windows of the same app means the same program (.exe), so every Chrome profile's windows are in one list.

INSTALL
  Double-click Install.cmd. It installs for your user only (no administrator rights),
  adds a Start menu shortcut, starts the app and starts it at every sign-in.
  Options (run install.ps1 from PowerShell):  -NoAutostart   -NoLaunch

REQUIREMENTS
  Windows 10 or 11 and the .NET 10 runtime (this test build is not self-contained).
  Missing it?  winget install Microsoft.DotNet.Runtime.10

SETTINGS
  Right-click (or click) the tray icon: preview on/off, preview delay, start with Windows.

IF ALT+` DOES NOTHING
  Another tool may own the hotkey (AltExten, an AutoHotkey script, ...). Whichever started last wins.
  Change that tool's window hotkey, or exit it.
  Windows running as administrator can't be switched from a normal app (a Windows security rule).

UNINSTALL
  Settings > Apps > Installed apps > Mohi AltSwitch > Uninstall,
  or run uninstall.ps1 from the install folder:  %LOCALAPPDATA%\Programs\Mohi AltSwitch

PRIVACY
  The app listens for the hotkey only. It does not record, store or send keystrokes, window titles
  or anything else, and makes no network connections.

Source: https://github.com/webgodo/mohi-altswitch
