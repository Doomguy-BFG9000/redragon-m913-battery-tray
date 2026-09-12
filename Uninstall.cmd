@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall.ps1"
if errorlevel 1 (
  echo.
  echo Uninstallation failed. See the error above.
  pause
  exit /b 1
)
echo.
echo Uninstallation completed.
exit /b 0
