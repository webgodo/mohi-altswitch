@echo off
rem Double-click to install Mohi AltSwitch for the current user (no administrator rights needed).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
if errorlevel 1 (
  echo.
  echo Installation did not complete. See the message above.
  pause
)
