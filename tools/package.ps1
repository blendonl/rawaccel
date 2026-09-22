param(
    [string]$Configuration = 'Release',
    [string]$OutDir = (Join-Path $PSScriptRoot '..\dist')
)

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$version = & (Join-Path $PSScriptRoot 'version.ps1')
$name = "rawaccel-$version-win64"
New-Item -ItemType Directory -Force $OutDir | Out-Null
$stage = Join-Path (Resolve-Path $OutDir).Path $name
$zip = "$stage.zip"

$requiredFiles = @(
    'rawaccel.exe'
    'writer.exe'
    'wrapper.dll'
    'Ijwhost.dll'
    'installer.exe'
    'uninstaller.exe'
    'driver\rawaccel.sys'
    'uninstall.ps1'
)

function Copy-BuildOutput {
    param([string]$Project)
    $bin = Join-Path $root "$Project\bin\x64\$Configuration"
    if (-not (Test-Path -LiteralPath $bin)) {
        throw "No $Configuration x64 build of $Project at $bin"
    }
    Copy-Item (Join-Path $bin '*') $stage -Recurse -Force
}

Remove-Item $stage, $zip -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $stage | Out-Null

Copy-BuildOutput 'grapher'
Copy-BuildOutput 'writer'
Get-ChildItem $stage -Recurse -Filter '*.pdb' | Remove-Item

$signed = Join-Path $root 'signed\x64'
Copy-Item (Join-Path $signed 'installer.exe'), (Join-Path $signed 'uninstaller.exe') $stage
Copy-Item (Join-Path $signed 'driver') $stage -Recurse
Copy-Item (Join-Path $root 'doc') $stage -Recurse
Copy-Item (Join-Path $root 'LICENSE'), (Join-Path $root 'ReadMe.md'), (Join-Path $root 'uninstall.ps1') $stage

$missing = $requiredFiles | Where-Object { -not (Test-Path -LiteralPath (Join-Path $stage $_)) }
if ($missing) {
    throw "The package is missing: $($missing -join ', ')"
}

Compress-Archive -Path $stage -DestinationPath $zip
Write-Host "Packaged $zip"
