param(
    [string]$Version,
    [string]$Package,
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\rawaccel'),
    [switch]$SkipDriver
)

function Get-RawAccelRelease {
    param([string]$Version)
    $tag = if ($Version) { "v$($Version.TrimStart('v'))" } else { $null }
    $endpoint = if ($tag) { "tags/$tag" } else { 'latest' }
    try {
        $found = Invoke-RestMethod "https://api.github.com/repos/blendonl/rawaccel/releases/$endpoint"
    } catch {
        throw "Could not find Raw Accel release $(if ($tag) { $tag } else { 'latest' }): $($_.Exception.Message)"
    }
    $asset = $found.assets | Where-Object name -like 'rawaccel-*-win64.zip' | Select-Object -First 1
    if (-not $asset) {
        throw "Release $($found.tag_name) has no rawaccel-*-win64.zip to install."
    }
    [pscustomobject]@{ Tag = $found.tag_name; Name = $asset.name; Url = $asset.browser_download_url; Path = $null }
}

function Get-LocalPackage {
    param([string]$Package)
    $file = Get-Item -LiteralPath $Package
    if ($file.Name -notmatch '^rawaccel-(.+)-win64\.zip$') {
        throw "$($file.Name) is not a rawaccel-<version>-win64.zip package."
    }
    [pscustomobject]@{ Tag = "v$($Matches[1])"; Name = $file.Name; Url = $null; Path = $file.FullName }
}

function Get-InstalledVersion {
    param([string]$Exe)
    if (Test-Path -LiteralPath $Exe -PathType Leaf) {
        (Get-Item -LiteralPath $Exe).VersionInfo.ProductVersion
    }
}

function Install-Prerequisite {
    param([string]$Name, [string]$Id, [string]$Url, [scriptblock]$Test)
    if (& $Test) {
        return
    }
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        throw "Raw Accel needs the $Name. Install it from $Url and run this again."
    }
    Write-Host "Installing the $Name"
    winget install --exact --id $Id --silent --accept-package-agreements --accept-source-agreements | Out-Host
    if (-not (& $Test)) {
        throw "The $Name did not install. Install it from $Url and run this again."
    }
}

function Test-VCRuntime {
    $runtime = Join-Path ([Environment]::SystemDirectory) 'msvcp140.dll'
    if (-not (Test-Path -LiteralPath $runtime)) {
        return $false
    }
    $info = (Get-Item -LiteralPath $runtime).VersionInfo
    [version]"$($info.FileMajorPart).$($info.FileMinorPart)" -ge [version]'14.44'
}

function Stop-InstalledRawAccel {
    param([string]$Directory)
    $running = @(Get-Process rawaccel, writer -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and (Split-Path $_.Path) -eq $Directory })
    if (-not $running) {
        return $false
    }
    $guiWasOpen = [bool]($running | Where-Object ProcessName -eq 'rawaccel')
    $running | ForEach-Object { [void]$_.CloseMainWindow() }
    $running | Wait-Process -Timeout 5 -ErrorAction SilentlyContinue
    $running | Where-Object { -not $_.HasExited } | Stop-Process -Force
    return $guiWasOpen
}

function Test-DriverCurrent {
    param([string]$Driver)
    $installed = Join-Path ([Environment]::SystemDirectory) 'drivers\rawaccel.sys'
    $mouseClass = 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e96f-e325-11ce-bfc1-08002be10318}'
    $filters = (Get-ItemProperty $mouseClass -ErrorAction SilentlyContinue).UpperFilters
    (Test-Path -LiteralPath $installed) -and
        (Test-Path 'HKLM:\SYSTEM\CurrentControlSet\Services\rawaccel') -and
        ($filters -contains 'rawaccel') -and
        ((Get-FileHash -LiteralPath $installed).Hash -eq (Get-FileHash -LiteralPath $Driver).Hash)
}

function Install-Driver {
    param([string]$Directory)
    $command = "Set-Location -LiteralPath '$($Directory.Replace("'", "''"))'; & .\installer.exe"
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
    Write-Host 'Installing the driver. Windows will ask for administrator rights.'
    try {
        Start-Process powershell.exe -Verb RunAs -Wait -ArgumentList '-NoProfile', '-EncodedCommand', $encoded
    } catch {
        Write-Warning "The driver installer did not run: $($_.Exception.Message)"
    }
}

function Add-StartMenuShortcut {
    param([string]$Exe)
    $path = Join-Path ([Environment]::GetFolderPath('Programs')) 'Raw Accel.lnk'
    $shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut($path)
    $shortcut.TargetPath = $Exe
    $shortcut.WorkingDirectory = Split-Path $Exe
    $shortcut.Description = 'Raw Accel mouse acceleration'
    $shortcut.Save()
}

function Register-UninstallEntry {
    param([string]$Directory, [string]$Version)
    $key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\RawAccel'
    $bytes = (Get-ChildItem -LiteralPath $Directory -Recurse -File | Measure-Object Length -Sum).Sum
    $uninstaller = Join-Path $Directory 'uninstall.ps1'
    New-Item $key -Force | Out-Null
    $strings = [ordered]@{
        DisplayName     = 'Raw Accel'
        DisplayVersion  = $Version
        Publisher       = 'Raw Accel'
        DisplayIcon     = Join-Path $Directory 'rawaccel.exe'
        InstallLocation = $Directory
        URLInfoAbout    = 'https://github.com/blendonl/rawaccel'
        UninstallString = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$uninstaller`""
    }
    foreach ($name in $strings.Keys) {
        Set-ItemProperty $key -Name $name -Value $strings[$name]
    }
    Set-ItemProperty $key -Name EstimatedSize -Value ([int]($bytes / 1KB)) -Type DWord
    Set-ItemProperty $key -Name NoModify -Value 1 -Type DWord
    Set-ItemProperty $key -Name NoRepair -Value 1 -Type DWord
}

function Install-RawAccel {
    param([string]$Version, [string]$Package, [string]$InstallDir, [switch]$SkipDriver)
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

    if ($env:PROCESSOR_ARCHITECTURE -ne 'AMD64') {
        throw 'Raw Accel is only released for x64 Windows. Run this from 64-bit PowerShell on an x64 PC.'
    }

    $InstallDir = [IO.Path]::GetFullPath($InstallDir).TrimEnd('\')
    $release = if ($Package) { Get-LocalPackage $Package } else { Get-RawAccelRelease $Version }
    $releaseVersion = $release.Tag.TrimStart('v')
    $exe = Join-Path $InstallDir 'rawaccel.exe'

    Install-Prerequisite -Name '.NET 10 runtime' -Id 'Microsoft.DotNet.Runtime.10' `
        -Url 'https://dotnet.microsoft.com/download/dotnet/10.0' `
        -Test { Test-Path (Join-Path $env:ProgramFiles 'dotnet\shared\Microsoft.NETCore.App\10.*') }
    Install-Prerequisite -Name 'Visual C++ runtime' -Id 'Microsoft.VCRedist.2015+.x64' `
        -Url 'https://aka.ms/vs/17/release/vc_redist.x64.exe' `
        -Test { Test-VCRuntime }

    $wasRunning = $false
    if (-not $Package -and (Get-InstalledVersion $exe) -eq $releaseVersion) {
        Write-Host "Raw Accel $($release.Tag) is already installed in $InstallDir"
    } else {
        $staging = Join-Path ([IO.Path]::GetTempPath()) "rawaccel-install-$([guid]::NewGuid())"
        New-Item -ItemType Directory $staging | Out-Null
        try {
            $zip = $release.Path
            if (-not $zip) {
                Write-Host "Downloading $($release.Name)"
                $zip = Join-Path $staging $release.Name
                Invoke-WebRequest $release.Url -OutFile $zip -UseBasicParsing
            }
            Expand-Archive $zip (Join-Path $staging 'unpacked')
            $payload = Get-ChildItem (Join-Path $staging 'unpacked') -Directory | Select-Object -First 1

            $wasRunning = Stop-InstalledRawAccel $InstallDir
            New-Item -ItemType Directory -Force $InstallDir | Out-Null
            Copy-Item (Join-Path $payload.FullName '*') $InstallDir -Recurse -Force
        } finally {
            Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
        }
        Write-Host "Installed Raw Accel $($release.Tag) to $InstallDir"
    }

    Add-StartMenuShortcut $exe
    Register-UninstallEntry $InstallDir $releaseVersion

    $driver = Join-Path $InstallDir 'driver\rawaccel.sys'
    $restartNeeded = $false
    if (-not $SkipDriver -and -not (Test-DriverCurrent $driver)) {
        Install-Driver $InstallDir
        if (Test-DriverCurrent $driver) {
            $restartNeeded = $true
        } else {
            Write-Warning "The driver is not installed. Run this again, or run installer.exe in $InstallDir as administrator."
        }
    }

    if ($wasRunning) {
        Start-Process $exe
    }

    if ($restartNeeded) {
        Write-Host 'Restart Windows to load the driver, then open Raw Accel from the Start menu.'
    } else {
        Write-Host 'Open Raw Accel from the Start menu.'
    }
}

Install-RawAccel -Version $Version -Package $Package -InstallDir $InstallDir -SkipDriver:$SkipDriver
