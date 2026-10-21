; VR Balance Walker installer (Inno Setup 6.7).
; Build: build.cmd  (or ISCC.exe VRBalanceWalker.iss)  ->  output\VRBalanceWalker-Setup-<version>.exe
; Needs the Release|x86 app build and steamvr_driver\build.bat output. vJoy is bundled only if
; redist\vJoySetup-<VJoyVersion>.exe exists (https://github.com/jshafer817/vJoy/releases/tag/v2.1.9.1).

#define AppName     "VR Balance Walker"
#define AppVersion  "1.0.0"
#define Src         RemoveBackslash(SourcePath) + "\.."
#define AppBin      Src + "\WiiBalanceWalker\bin\x86\Release"
#define Drv         Src + "\steamvr_driver\wbw"
#define VJoyVersion "2.1.9.1"
#define VJoySetup   "redist\vJoySetup-" + VJoyVersion + ".exe"
#define HaveVJoy    FileExists(AddBackslash(SourcePath) + VJoySetup)

[Setup]
AppId={{6B0E2C1A-7F3D-4E8B-9A51-5C2D8E4F7B10}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppName}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; The SteamVR driver is 64-bit only, so there is no point installing on 32-bit Windows.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
WizardStyle=modern dynamic
SetupIconFile=assets\wbw.ico
WizardSmallImageFile=assets\wizard-small.png
UninstallDisplayIcon={app}\wbw.ico
UninstallDisplayName={#AppName}
OutputDir=output
OutputBaseFilename=VRBalanceWalker-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
CloseApplications=yes
RestartApplications=no

[Types]
#if HaveVJoy
Name: "full";   Description: "Full (app + SteamVR driver + vJoy)"
#else
Name: "full";   Description: "Full (app + SteamVR driver)"
#endif
Name: "vr";     Description: "VR only (app + SteamVR driver)"
Name: "custom"; Description: "Custom"; Flags: iscustom

[Components]
Name: "app";     Description: "VR Balance Walker app";                                           Types: full vr custom; Flags: fixed
Name: "steamvr"; Description: "SteamVR treadmill driver (walk/turn in VR games like VRChat)";   Types: full vr custom
#if HaveVJoy
Name: "vjoy";    Description: "vJoy {#VJoyVersion} virtual joystick driver (flat-screen / desktop games)"; Types: full
#endif

[Tasks]
Name: "multidrivers"; Description: "Turn on SteamVR ""activate multiple drivers"" (needed with Quest Link, Virtual Desktop, most headsets)"; Components: steamvr
Name: "desktopicon";  Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Dirs]
; The app writes wbw_errors.log next to the exe, and the SteamVR helper writes its log here too.
Name: "{app}"; Permissions: users-modify

[Files]
Source: "{#AppBin}\WiiBalanceWalker.exe";        DestDir: "{app}"; Components: app; Flags: ignoreversion
Source: "{#AppBin}\WiiBalanceWalker.exe.config"; DestDir: "{app}"; Components: app; Flags: ignoreversion
Source: "{#AppBin}\WiimoteLib.dll";              DestDir: "{app}"; Components: app; Flags: ignoreversion
Source: "{#AppBin}\InTheHand.Net.Personal.dll";  DestDir: "{app}"; Components: app; Flags: ignoreversion
Source: "{#AppBin}\vJoyInterface.dll";           DestDir: "{app}"; Components: app; Flags: ignoreversion
Source: "{#AppBin}\vJoyInterfaceWrap.dll";       DestDir: "{app}"; Components: app; Flags: ignoreversion
Source: "{#AppBin}\README.txt";                  DestDir: "{app}"; DestName: "WiiBalanceWalker-original-README.txt"; Components: app; Flags: ignoreversion
Source: "README.txt";                            DestDir: "{app}"; Components: app; Flags: ignoreversion isreadme
Source: "assets\wbw.ico";                        DestDir: "{app}"; Components: app; Flags: ignoreversion

Source: "steamvr-setup.ps1";            DestDir: "{app}"; Components: steamvr; Flags: ignoreversion
Source: "register-steamvr-driver.cmd";  DestDir: "{app}"; Components: steamvr; Flags: ignoreversion
Source: "{#Drv}\driver.vrdrivermanifest";                     DestDir: "{app}\steamvr\wbw";                        Components: steamvr; Flags: ignoreversion
Source: "{#Drv}\bin\win64\driver_wbw.dll";                    DestDir: "{app}\steamvr\wbw\bin\win64";              Components: steamvr; Flags: ignoreversion
Source: "{#Drv}\resources\input\*.json";                      DestDir: "{app}\steamvr\wbw\resources\input";        Components: steamvr; Flags: ignoreversion
Source: "{#Drv}\resources\localization\localization.json";    DestDir: "{app}\steamvr\wbw\resources\localization"; Components: steamvr; Flags: ignoreversion
Source: "{#Drv}\resources\settings\default.vrsettings";       DestDir: "{app}\steamvr\wbw\resources\settings";     Components: steamvr; Flags: ignoreversion
Source: "{#Drv}\resources\icons\wbw_board.svg";               DestDir: "{app}\steamvr\wbw\resources\icons";        Components: steamvr; Flags: ignoreversion
Source: "{#Drv}\resources\icons\wbw_status_on.png";           DestDir: "{app}\steamvr\wbw\resources\icons";        Components: steamvr; Flags: ignoreversion
Source: "{#Drv}\resources\icons\wbw_status_on@2x.png";        DestDir: "{app}\steamvr\wbw\resources\icons";        Components: steamvr; Flags: ignoreversion
Source: "{#Drv}\resources\icons\wbw_status_standby.png";      DestDir: "{app}\steamvr\wbw\resources\icons";        Components: steamvr; Flags: ignoreversion
Source: "{#Drv}\resources\icons\wbw_status_standby@2x.png";   DestDir: "{app}\steamvr\wbw\resources\icons";        Components: steamvr; Flags: ignoreversion
Source: "{#Drv}\resources\icons\wbw_status_error.png";        DestDir: "{app}\steamvr\wbw\resources\icons";        Components: steamvr; Flags: ignoreversion

#if HaveVJoy
Source: "{#VJoySetup}"; DestDir: "{tmp}"; Components: vjoy; Flags: deleteafterinstall; Check: not VJoyInstalled
#endif

[Icons]
Name: "{group}\{#AppName}";               Filename: "{app}\WiiBalanceWalker.exe"; IconFilename: "{app}\wbw.ico"
Name: "{group}\README";                   Filename: "{app}\README.txt"
Name: "{group}\Register SteamVR driver";  Filename: "{app}\register-steamvr-driver.cmd"; IconFilename: "{app}\wbw.ico"; Components: steamvr
Name: "{group}\Configure vJoy";           Filename: "{commonpf64}\vJoy\x64\vJoyConf.exe"; Check: VJoyConfExists
Name: "{group}\Uninstall {#AppName}";     Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";         Filename: "{app}\WiiBalanceWalker.exe"; IconFilename: "{app}\wbw.ico"; Tasks: desktopicon

[Run]
#if HaveVJoy
Filename: "{tmp}\vJoySetup-{#VJoyVersion}.exe"; Parameters: "/SILENT /SUPPRESSMSGBOXES /NORESTART"; StatusMsg: "Installing vJoy {#VJoyVersion} (Windows may ask to trust the driver)..."; Components: vjoy; Check: not VJoyInstalled
#endif
Filename: "{app}\WiiBalanceWalker.exe"; Description: "Start {#AppName}"; Flags: postinstall nowait skipifsilent runasoriginaluser

[UninstallRun]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\steamvr-setup.ps1"" -Uninstall -DriverDir ""{app}\steamvr\wbw"""; Flags: runhidden; RunOnceId: "UnregisterWbwDriver"; Components: steamvr

[UninstallDelete]
Type: files; Name: "{app}\wbw_errors.log"
Type: files; Name: "{app}\steamvr-setup.log"
Type: files; Name: "{app}\WiiBalanceWalker.exe.running-old*"

[Code]
function VJoyInstalled: Boolean;
begin
  Result := RegKeyExists(HKLM64, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{8E31F76F-74C3-47F1-9550-E041EEDC5FBB}_is1')
    or FileExists(ExpandConstant('{commonpf64}\vJoy\x64\vJoyConf.exe'));
end;

function VJoyConfExists: Boolean;
begin
  Result := FileExists(ExpandConstant('{commonpf64}\vJoy\x64\vJoyConf.exe'));
end;

function IsProcessRunning(const Name: String): Boolean;
var
  Locator, Service, Procs: Variant;
begin
  Result := False;
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Service := Locator.ConnectServer('.', 'root\CIMV2');
    Procs := Service.ExecQuery('SELECT ProcessId FROM Win32_Process WHERE Name="' + Name + '"');
    Result := Procs.Count > 0;
  except
  end;
end;

function SteamVRRunning: Boolean;
begin
  Result := IsProcessRunning('vrserver.exe') or IsProcessRunning('vrmonitor.exe') or IsProcessRunning('vrcompositor.exe');
end;

{ SteamVR keeps driver_wbw.dll loaded and rewrites steamvr.vrsettings on exit, so it has to be closed. }
function WaitForSteamVRClosed(const Doing: String): Boolean;
begin
  Result := True;
  while SteamVRRunning do
  begin
    if SuppressibleMsgBox('SteamVR is running. Close SteamVR (and the headset''s SteamVR session) so ' + Doing +
        ', then click Retry.', mbInformation, MB_RETRYCANCEL, IDCANCEL) = IDCANCEL then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

procedure InitializeWizard;
begin
#if HaveVJoy
  if VJoyInstalled then
    WizardSelectComponents('!vjoy');
#endif
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if WizardIsComponentSelected('steamvr') and not WaitForSteamVRClosed('the treadmill driver can be installed') then
    Result := 'SteamVR is still running. Close SteamVR and run the installer again.';
end;

procedure RegisterSteamVRDriver;
var
  Params, LogFile: String;
  ResultCode: Integer;
begin
  LogFile := ExpandConstant('{app}\steamvr-setup.log');
  Params := '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\steamvr-setup.ps1') + '" -Install' +
            ' -DriverDir "' + ExpandConstant('{app}\steamvr\wbw') + '" -LogFile "' + LogFile + '"';
  if WizardIsTaskSelected('multidrivers') then
    Params := Params + ' -MultipleDrivers';

  WizardForm.StatusLabel.Caption := 'Registering the SteamVR driver...';
  { Run as the logged-in user: SteamVR's openvrpaths.vrpath lives in that user's %LOCALAPPDATA%. }
  if not ExecAsOriginalUser('powershell.exe', Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    ResultCode := 1;

  case ResultCode of
    0: ;
    2: SuppressibleMsgBox('SteamVR was not found for this Windows account (it has to be installed and started at least once).' + #13#10#13#10 +
         'The app is installed. After you have started SteamVR once and closed it, run "Register SteamVR driver" from the Start menu.',
         mbInformation, MB_OK, IDOK);
    3: SuppressibleMsgBox('The driver was registered, but SteamVR was running so its settings were not changed.' + #13#10#13#10 +
         'Close SteamVR and run "Register SteamVR driver" from the Start menu.', mbInformation, MB_OK, IDOK);
  else
    SuppressibleMsgBox('Registering the SteamVR driver failed. Details are in:' + #13#10 + LogFile + #13#10#13#10 +
      'You can retry with "Register SteamVR driver" in the Start menu, or follow the manual steps in the README.',
      mbError, MB_OK, IDOK);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and WizardIsComponentSelected('steamvr') then
    RegisterSteamVRDriver;
end;

function InitializeUninstall: Boolean;
begin
  Result := WaitForSteamVRClosed('the treadmill driver can be removed');
end;
