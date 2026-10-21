# Registers / unregisters the VR Balance Walker SteamVR driver ("wbw") and, optionally, turns on
# steamvr.activateMultipleDrivers so the treadmill driver loads next to the headset driver (Quest Link, Index, ...).
#
#   steamvr-setup.ps1 -Install   -DriverDir "C:\Program Files (x86)\VR Balance Walker\steamvr\wbw" [-MultipleDrivers]
#   steamvr-setup.ps1 -Uninstall -DriverDir "..."
#   add -DryRun to only print what would change.
#
# Exit codes: 0 ok, 2 SteamVR not found / never run, 3 SteamVR is running (settings not edited), 1 other error.
param(
    [switch]$Install,
    [switch]$Uninstall,
    [string]$DriverDir = (Join-Path $PSScriptRoot 'steamvr\wbw'),
    [switch]$MultipleDrivers,
    [switch]$DryRun,
    [string]$LogFile
)

$ErrorActionPreference = 'Stop'
$script:exitCode = 0

function Log([string]$msg) {
    Write-Host $msg
    if ($LogFile) { try { Add-Content -LiteralPath $LogFile -Value ("[{0:yyyy-MM-dd HH:mm:ss}] {1}" -f (Get-Date), $msg) } catch { } }
}

function Norm([string]$p) {
    if (-not $p) { return '' }
    try { $p = [IO.Path]::GetFullPath($p) } catch { }
    return $p.TrimEnd('\').ToLowerInvariant()
}

function Read-Json([string]$path) {
    # Windows PowerShell 5.1 ConvertFrom-Json chokes on a UTF-8 BOM in some cases, so strip it.
    $text = [IO.File]::ReadAllText($path).TrimStart([char]0xFEFF)
    return $text | ConvertFrom-Json
}

function Write-Json($obj, [string]$path) {
    $json = $obj | ConvertTo-Json -Depth 64
    [IO.File]::WriteAllText($path, $json, (New-Object Text.UTF8Encoding($false)))
}

function Set-Prop($obj, [string]$name, $value) {
    if ($obj.PSObject.Properties[$name]) { $obj.$name = $value }
    else { $obj | Add-Member -NotePropertyName $name -NotePropertyValue $value }
}

function Invoke-VrPathReg([string]$exe, [string[]]$arguments) {
    Log ("  vrpathreg " + ($arguments -join ' '))
    if ($DryRun) { return }
    $out = & $exe @arguments 2>&1
    if ($out) { Log ("    " + ($out -join "`n    ")) }
    if ($LASTEXITCODE -ne 0) { Log "    (vrpathreg exit code $LASTEXITCODE)" }
}

function Get-SteamVrRunning {
    return @(Get-Process -Name vrserver, vrmonitor, vrcompositor -ErrorAction SilentlyContinue).Count -gt 0
}

try {
    if (-not ($Install -xor $Uninstall)) { throw 'Pass exactly one of -Install or -Uninstall.' }
    $DriverDir = [IO.Path]::GetFullPath($DriverDir).TrimEnd('\')
    Log ("VR Balance Walker SteamVR setup: " + $(if ($Install) { 'install' } else { 'uninstall' }) + " ($DriverDir)" + $(if ($DryRun) { ' [dry run]' } else { '' }))

    $pathsFile = Join-Path $env:LOCALAPPDATA 'openvr\openvrpaths.vrpath'
    if (-not (Test-Path -LiteralPath $pathsFile)) {
        Log "SteamVR has not been run on this Windows account yet ($pathsFile is missing)."
        Log "Install SteamVR from Steam, start it once, close it, then run 'Register SteamVR driver' from the Start menu."
        exit 2
    }
    $paths = Read-Json $pathsFile
    $runtime = @($paths.runtime) | Where-Object { $_ -and (Test-Path -LiteralPath (Join-Path $_ 'bin\win64\vrpathreg.exe')) } | Select-Object -First 1
    if (-not $runtime) {
        Log "Couldn't find SteamVR's vrpathreg.exe (runtime paths: $(@($paths.runtime) -join ', '))."
        Log "Make sure SteamVR is installed, start it once, close it, then run 'Register SteamVR driver' from the Start menu."
        exit 2
    }
    $vrpathreg = Join-Path $runtime 'bin\win64\vrpathreg.exe'
    Log "SteamVR: $runtime"

    $mine = Norm $DriverDir
    $registered = @($paths.external_drivers) | Where-Object { $_ }

    if ($Install) {
        if (-not (Test-Path -LiteralPath (Join-Path $DriverDir 'driver.vrdrivermanifest'))) { throw "No driver.vrdrivermanifest in $DriverDir" }

        # Drop other copies of the "wbw" driver (an older install, a dev build, a deleted folder): two drivers with the
        # same name make SteamVR load whichever it finds first.
        foreach ($p in $registered) {
            if ((Norm $p) -eq $mine) { continue }
            $manifest = Join-Path $p 'driver.vrdrivermanifest'
            $isWbw = $false
            if (Test-Path -LiteralPath $manifest) {
                try { $isWbw = ((Read-Json $manifest).name -eq 'wbw') } catch { }
            } elseif ((Split-Path $p -Leaf) -eq 'wbw') {
                $isWbw = $true   # stale entry pointing at a folder that no longer exists
            }
            if ($isWbw) {
                Log "Removing other wbw driver registration: $p"
                Invoke-VrPathReg $vrpathreg @('removedriver', $p)
            }
        }

        if (@($registered | Where-Object { (Norm $_) -eq $mine }).Count -eq 0) {
            Log "Registering driver."
            Invoke-VrPathReg $vrpathreg @('adddriver', $DriverDir)
        } else {
            Log "Driver already registered."
        }

        # steamvr.vrsettings: activateMultipleDrivers, and re-enable the add-on if SteamVR switched it off earlier.
        $configDir = @($paths.config) | Where-Object { $_ } | Select-Object -First 1
        if (-not $configDir) { Log "No SteamVR config folder listed in openvrpaths.vrpath; skipping settings." }
        else {
            $settingsFile = Join-Path $configDir 'steamvr.vrsettings'
            if (Get-SteamVrRunning) {
                Log "SteamVR is running, so steamvr.vrsettings was NOT edited (SteamVR would overwrite it on exit)."
                Log "Close SteamVR and run 'Register SteamVR driver' from the Start menu, or change the settings by hand (see README)."
                $script:exitCode = 3
            } else {
                if (Test-Path -LiteralPath $settingsFile) { $settings = Read-Json $settingsFile }
                else { $settings = New-Object PSObject }
                $changed = $false

                if ($MultipleDrivers) {
                    if (-not $settings.PSObject.Properties['steamvr']) { Set-Prop $settings 'steamvr' (New-Object PSObject) }
                    if ($settings.steamvr.activateMultipleDrivers -ne $true) {
                        Log "Setting steamvr.activateMultipleDrivers = true"
                        Set-Prop $settings.steamvr 'activateMultipleDrivers' $true
                        $changed = $true
                    } else { Log "steamvr.activateMultipleDrivers is already true." }
                }
                if ($settings.PSObject.Properties['driver_wbw'] -and $settings.driver_wbw.PSObject.Properties['enable'] -and $settings.driver_wbw.enable -ne $true) {
                    Log "Re-enabling the wbw add-on (driver_wbw.enable was false)."
                    $settings.driver_wbw.enable = $true
                    $changed = $true
                }

                if ($changed -and -not $DryRun) {
                    if (Test-Path -LiteralPath $settingsFile) {
                        $backup = "$settingsFile.bak-vrbalancewalker"
                        Copy-Item -LiteralPath $settingsFile -Destination $backup -Force
                        Log "Backup: $backup"
                    }
                    Write-Json $settings $settingsFile
                    Log "Wrote $settingsFile"
                }
            }
        }
    } else {
        foreach ($p in $registered) {
            if ((Norm $p) -eq $mine) {
                Log "Unregistering driver."
                Invoke-VrPathReg $vrpathreg @('removedriver', $p)
            }
        }
    }
    Log "Done."
    exit $script:exitCode
} catch {
    Log ("ERROR: " + $_.Exception.Message)
    exit 1
}
