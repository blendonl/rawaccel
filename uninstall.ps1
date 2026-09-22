param([switch]$KeepDriver)

function Test-DriverInstalled {
    $mouseClass = 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e96f-e325-11ce-bfc1-08002be10318}'
    $filters = (Get-ItemProperty $mouseClass -ErrorAction SilentlyContinue).UpperFilters
    ($filters -contains 'rawaccel') -or
        (Test-Path -LiteralPath (Join-Path ([Environment]::SystemDirectory) 'drivers\rawaccel.sys'))
}

function Stop-RawAccel {
    param([string]$Directory)
    $running = @(Get-Process rawaccel, writer -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and (Split-Path $_.Path) -eq $Directory })
    if (-not $running) {
        return
    }
    $running | ForEach-Object { [void]$_.CloseMainWindow() }
    $running | Wait-Process -Timeout 5 -ErrorAction SilentlyContinue
    $running | Where-Object { -not $_.HasExited } | Stop-Process -Force
}

function Uninstall-RawAccel {
    param([string]$Directory, [switch]$KeepDriver)
    $ErrorActionPreference = 'Stop'

    Stop-RawAccel $Directory

    $driverRemoved = $false
    if (-not $KeepDriver -and (Test-DriverInstalled)) {
        Write-Host 'Removing the driver. Windows will ask for administrator rights.'
        try {
            Start-Process (Join-Path $Directory 'uninstaller.exe') -Verb RunAs -Wait
        } catch {
            throw "The driver was not removed, so Raw Accel was left installed: $($_.Exception.Message)"
        }
        if (Test-DriverInstalled) {
            throw 'The driver is still installed, so Raw Accel was left installed.'
        }
        $driverRemoved = $true
    }

    Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) 'Raw Accel.lnk') -ErrorAction SilentlyContinue
    Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\RawAccel' -Recurse -ErrorAction SilentlyContinue

    $elsewhere = [IO.Path]::GetTempPath()
    Set-Location -LiteralPath $elsewhere
    [Environment]::CurrentDirectory = $elsewhere
    Remove-Item -LiteralPath $Directory -Recurse -Force

    Write-Host 'Raw Accel is uninstalled.'
    if ($driverRemoved) {
        Write-Host 'Restart Windows to finish removing the driver.'
    }
}

Uninstall-RawAccel -Directory $PSScriptRoot -KeepDriver:$KeepDriver
