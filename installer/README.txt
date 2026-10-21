VR BALANCE WALKER
=================

Stand on a Wii Balance Board and lean to walk, twist to turn. Works in SteamVR games
(VRChat and others) through its own treadmill driver, and in flat-screen games
through vJoy, a virtual joystick.

Based on WiiBalanceWalker v0.5 by Shachar Liberman (v0.4 and earlier by Richard Perry,
GreyCube.com), released under the Microsoft Public License. The original readme is
in the install folder as WiiBalanceWalker-original-README.txt.

Button and switch names in this readme are from the default Aero theme. The THEME
button in the title bar switches themes. In the "Debloated" theme a few switches have
longer names, which are given in brackets.


CONTENTS
  1. What the installer puts on your PC
  2. Connecting the balance board
  3. SteamVR setup (VR games)
  4. VRChat
  5. Other SteamVR games
  6. Flat-screen games (vJoy)
  7. Tuning
  8. Troubleshooting
  9. Uninstalling


1. WHAT THE INSTALLER PUTS ON YOUR PC
-------------------------------------
  C:\Program Files\VR Balance Walker\
      WiiBalanceWalker.exe          the app (the window is titled "VR Balance Walker")
      steamvr\wbw\                  SteamVR treadmill driver "wbw"
      register-steamvr-driver.cmd   re-runs the SteamVR setup (also in the Start menu)
      steamvr-setup.log             what the SteamVR setup changed

  Optional: vJoy 2.1.9.1 virtual joystick driver (skipped if vJoy is already installed).

  Windows 10/11 64-bit and Bluetooth are required. .NET Framework 4 ships with Windows.

  If the installer ran the SteamVR setup without errors, you can skip to section 4.


2. CONNECTING THE BALANCE BOARD
-------------------------------
  1. Take the battery cover off the board. The red SYNC button is next to the batteries.
  2. In the app, click "Pair / remove Bluetooth device" and follow the steps. You can also
     use Windows Settings > Bluetooth & devices > Add device. The board shows up as
     "Nintendo RVL-WBC-01". If Windows asks for a PIN, skip it and leave it blank.
     Pair it from Windows Bluetooth settings, not the old Control Panel. Otherwise you
     will have to pair again every time.
  3. Click "Connect to balance board". The button changes to "Board connected" once it
     is connected.
  4. Step off the board and click "Tare balance board". Then step on, stand normally and
     click "Set current balance as center".

  Under Startup you can turn on "Connect on launch" and "Tare after connecting".


3. STEAMVR SETUP (VR GAMES)
---------------------------
The board shows up in SteamVR as a treadmill controller called "Wii Balance Board". The
app sends it data over UDP on 127.0.0.1 port 26790 (this computer only, so the firewall
doesn't matter).

3a. What the installer already did for you
  - Registered the driver folder with SteamVR. This is the same as running:
        "<SteamVR>\bin\win64\vrpathreg.exe" adddriver "C:\Program Files\VR Balance Walker\steamvr\wbw"
    It also removed any other copy of the "wbw" driver that was registered (old installs,
    dev builds).
  - If you left the box ticked, it turned on activateMultipleDrivers in
    Steam\config\steamvr.vrsettings, after making a backup called
    steamvr.vrsettings.bak-vrbalancewalker.
  - If SteamVR had switched the add-on off earlier, it turned it back on.

  You can see exactly what changed in steamvr-setup.log in the install folder.

  If SteamVR wasn't installed yet, or had never been started on this Windows account, the
  installer could not do this. Start SteamVR once, close it, then run
  Start menu > VR Balance Walker > Register SteamVR driver.

3b. Doing it by hand
  Close SteamVR first. SteamVR overwrites its settings file when it exits, so edits made
  while it is running are lost.

  1. Register the driver. Open a command prompt and run:
        "C:\Program Files (x86)\Steam\steamapps\common\SteamVR\bin\win64\vrpathreg.exe" adddriver "C:\Program Files\VR Balance Walker\steamvr\wbw"
     Run "vrpathreg.exe show" to check that it is listed under External Drivers.

  2. Turn on "activate multiple drivers". Open
        C:\Program Files (x86)\Steam\config\steamvr.vrsettings
     in Notepad. Inside the "steamvr" : { ... } block, add this line:
        "activateMultipleDrivers" : true,
     Without it, SteamVR only loads your headset's driver (Oculus/Quest Link, Virtual
     Desktop, Lighthouse, ...) and the treadmill never appears.

  3. Start SteamVR and open Settings > Startup / Shutdown > Manage Add-Ons. Make sure
     "wbw" is set to On. If SteamVR ever crashed while loading add-ons, it may have
     switched it off.

3c. Checking that it works
  - In the app, turn on "SteamVR treadmill driver" in the Outputs panel
    ["Send to SteamVR (wbw treadmill driver)"]. You don't need vJoy for VR.
  - The SteamVR status window should show an extra device with a face icon:
        outline face = the driver is loaded but no data is coming in (the app is closed,
                       the box is unticked, or the board isn't connected)
        filled face  = live, the board is sending data
    If no input arrives for half a second, the driver sends zero, so you stop walking
    when the app closes.
  - In VR, open SteamVR Settings > Controllers > Test Controller (or Manage Controller
    Bindings) and pick "Wii Balance Board". It has three inputs:
        Lean          joystick   leaning forward/back/left/right
        Twist         joystick   twisting your hips; X = turn
        Board Button  button     the board's front power button

  If you reinstall or upgrade, close SteamVR first. SteamVR keeps the driver DLL locked
  while it runs.


4. VRCHAT
---------
  The driver comes with a VRChat binding: Lean = Move, Twist = Turn, Board Button = Jump.
  SteamVR uses it automatically. You don't need to set anything up.

  - Run VRChat through SteamVR. Launch it from Steam in SteamVR mode. The Meta/Oculus
    store version or Oculus mode won't see SteamVR devices.
  - Twist uses VRChat's normal turn action, so VRChat's comfort/snap turning setting
    applies. Turn snap turning off if you want smooth twist turning.
  - If you changed VRChat's bindings before and walking does nothing: in SteamVR go to
    Settings > Controllers > Manage Controller Bindings > VRChat, choose the
    "Wii Balance Board" controller, and select the binding "WiiBalanceWalker for VRChat".
  - Your Quest/Index controller sticks still work at the same time.


5. OTHER STEAMVR GAMES
----------------------
  Only VRChat has a binding that is set up automatically. For any other game:

  1. Start the game. Then in SteamVR go to Settings > Controllers > Manage Controller
     Bindings, pick the game, and choose the "Wii Balance Board" controller.
  2. Bind Lean (joystick) to the game's move/locomotion action, and Twist to its
     turn/rotate action, if it has one. Save.

  Catch: SteamVR treats the board as a "treadmill". That is a secondary device, and many
  games only read movement from the left-hand controller. If you bound it and the game
  still ignores the board, the binding needs the option returnBindingsWithLeftHand. Use
  the VRChat binding as a template:
        steamvr\wbw\resources\input\vrchat_bindings_wbw_board.json
  Copy it with a new name, then change "app_key" to the game's key (steam.app.<AppID>) and
  change the action paths to the game's actions. You can see those in the game's own
  binding. Then add the file to "default_bindings" in
        steamvr\wbw\resources\input\wbw_board_profile.json
  and restart SteamVR.


6. FLAT-SCREEN GAMES (vJoy)
---------------------------
  1. Install vJoy (the installer does this if you picked it). Start menu > Configure vJoy
     should show device 1 with at least the X, Y and Z axes enabled. That is the default.
  2. In the app's Outputs panel, turn on "vJoy output" ["Enable vJoy output"] and
     "Lean -> X / Y axes" ["Send CG to X/Y axes"]. To turn, also turn on
     "Twist turns (Z axis)" in the Twist to Turn panel ["Send twist to Z axis"].
        X = lean left/right    Y = lean forward/back    Z = twist (shows up as the right
        stick X in browsers and in most games that use SDL)
  3. Games that read DirectInput joysticks can use it right away.

  Steam games (and VRChat in desktop mode) only see vJoy as a gamepad if Steam has a
  mapping for it. Steam's "Define Layout" wizard doesn't work here because vJoy has no
  buttons, so you have to add the mapping by hand:
     a. Exit Steam completely.
     b. Back up C:\Program Files (x86)\Steam\config\config.vdf, then open it in Notepad.
     c. Find "SDL_GamepadBind". It sits directly inside "InstallConfigStore". If it isn't
        there, add it as a new line right after the opening { of InstallConfigStore.
        Inside its quoted value, add these two lines, each ending with a comma:
          0300000034120000adbe000019020000,vJoy - Virtual Joystick,leftx:a0,lefty:a1,rightx:a2,a:b0,platform:Windows,
          0300000034120000adbe000000000000,vJoy - Virtual Joystick,leftx:a0,lefty:a1,rightx:a2,a:b0,platform:Windows,
     d. Start Steam. Turn on Settings > Controller > "Generic Controller" support (Steam
        Input), and turn Steam Input on in the game's own Properties > Controller.


7. TUNING
---------
  Lean to move
      Sensitivity    default 2.0x. Raise it if you have to lean a long way to reach
                     full speed.
      Deadzone       default 0%. Raise it a little if you drift while standing still.
  Twist to turn
      Deadzone / Full turn at   default 4% / 15%.
      Sensitivity               default 2.0x, applied before the deadzone.
  Walk in place
      Step in place to walk forward. While this is on, leaning sideways to strafe is
      ignored, so for VRChat-style lean control leave it off.

  "Disable all keyboard / mouse actions" only switches off the old keyboard/mouse
  emulation. It doesn't affect vJoy or SteamVR output.


8. TROUBLESHOOTING
------------------
  No "Wii Balance Board" device in SteamVR
      - Run Start menu > Register SteamVR driver with SteamVR closed, then start SteamVR.
      - Check that wbw is On in Settings > Startup / Shutdown > Manage Add-Ons.
      - Check that activateMultipleDrivers is true (section 3b).
      - Look in C:\Program Files (x86)\Steam\logs\vrserver.txt for lines that start
        with "wbw". "listening on 127.0.0.1:26790" means the driver loaded.
        "could not bind UDP port" means another program is using port 26790.

  The device is there but the face is an outline
      The app isn't sending. Turn on "SteamVR treadmill driver", and make sure the
      connect button says "Board connected".

  You walk when standing still, or walk in the wrong direction
      Step off and Tare, step on and "Set current balance as center". If you still drift,
      raise the Lean Deadzone.

  The app closes by itself
      Errors are logged to wbw_errors.log in the install folder.

  Settings from an older portable copy are missing
      Windows stores app settings per exe location, so the installed copy starts with
      default settings. Set them up again once.


9. UNINSTALLING
---------------
  Use Settings > Apps > VR Balance Walker > Uninstall, or the Start menu entry. Close
  SteamVR first. The uninstaller unregisters the SteamVR driver. It leaves
  activateMultipleDrivers turned on, because other add-ons may need it, and it doesn't
  remove vJoy. vJoy has its own entry in Apps if you want to remove it.


CREDITS
-------
  WiiBalanceWalker   Shachar Liberman (v0.5), Richard Perry / GreyCube.com (v0.4 and
                     earlier). Microsoft Public License.
  WiimoteLib         lshachar's fork of Brian Peek's WiimoteLib
  32feet.NET         Bluetooth library (InTheHand.Net.Personal.dll)
  vJoy               Shaul Eizikovich; 2.1.9.1 build signed by Justin Shafer. MIT license.
  OpenVR             Valve Corporation (driver API header)
