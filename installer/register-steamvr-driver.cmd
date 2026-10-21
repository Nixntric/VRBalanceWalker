@echo off
rem Re-registers the VR Balance Walker SteamVR driver. Close SteamVR first.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0steamvr-setup.ps1" -Install -DriverDir "%~dp0steamvr\wbw" -MultipleDrivers -LogFile "%~dp0steamvr-setup.log"
echo.
pause
