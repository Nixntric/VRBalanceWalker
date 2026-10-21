@echo off
rem Builds installer\output\VRBalanceWalker-Setup-<version>.exe with Inno Setup 6.
rem Build the app (Release|x86) and steamvr_driver\build.bat first.
setlocal
set "ISCC_EXE=%ISCC%"
if not defined ISCC_EXE if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC_EXE=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not defined ISCC_EXE if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC_EXE=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not defined ISCC_EXE if exist "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" set "ISCC_EXE=%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"
if not defined ISCC_EXE if exist "%~dp0..\..\tools\InnoSetup6\ISCC.exe" set "ISCC_EXE=%~dp0..\..\tools\InnoSetup6\ISCC.exe"
if not defined ISCC_EXE (
    echo Inno Setup 6 not found. Install it from https://jrsoftware.org/isdl.php or set ISCC=path\to\ISCC.exe
    exit /b 1
)
if not exist "%~dp0redist\vJoySetup-2.1.9.1.exe" echo Note: redist\vJoySetup-2.1.9.1.exe not found, building without the vJoy component.
"%ISCC_EXE%" /Q "%~dp0VRBalanceWalker.iss" || exit /b 1
copy /y "%~dp0README.txt" "%~dp0output\README.txt" >nul
echo Built %~dp0output
