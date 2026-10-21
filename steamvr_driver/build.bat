@echo off
rem Builds wbw\bin\win64\driver_wbw.dll with the newest Visual Studio / Build Tools that has the x64 C++ compiler.
setlocal
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (echo Visual Studio Build Tools with C++ not found. & exit /b 1)
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSDIR=%%i"
if not defined VSDIR (echo No Visual Studio install with the x64 C++ tools found. & exit /b 1)
call "%VSDIR%\VC\Auxiliary\Build\vcvars64.bat" >nul || exit /b 1
cd /d "%~dp0"
if not exist wbw\bin\win64 mkdir wbw\bin\win64
cl /nologo /O2 /EHsc /MT /LD /std:c++17 src\driver_wbw.cpp /Fo:"%TEMP%\\" /Fe:wbw\bin\win64\driver_wbw.dll /link ws2_32.lib || exit /b 1
del wbw\bin\win64\driver_wbw.exp wbw\bin\win64\driver_wbw.lib 2>nul
echo Built wbw\bin\win64\driver_wbw.dll
