[CmdletBinding()]
param(
    [switch]$NoStart
)

$ErrorActionPreference = 'Stop'

if (-not [Environment]::Is64BitOperatingSystem) {
    throw 'This release requires 64-bit Windows 10 or Windows 11.'
}
if ([Environment]::OSVersion.Version.Major -lt 10 -or
    [Environment]::OSVersion.Version.Build -lt 17763) {
    throw 'This release requires Windows 10 version 1809 or newer.'
}

$sourceExe = Join-Path $PSScriptRoot 'RedragonBatteryTray.exe'
$installFolder = Join-Path $env:LOCALAPPDATA 'Programs\RedragonM913BatteryTray'
$installedExe = Join-Path $installFolder 'RedragonBatteryTray.exe'

if (-not (Test-Path -LiteralPath $sourceExe -PathType Leaf)) {
    throw 'Extract the complete ZIP first. RedragonBatteryTray.exe must be next to Install.ps1.'
}

$running = Get-Process RedragonBatteryTray -ErrorAction SilentlyContinue |
    Where-Object {
        try { $_.Path -eq $installedExe } catch { $false }
    }
if ($running) {
    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 5 -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Force -Path $installFolder | Out-Null
$filesToInstall = @(
    'RedragonBatteryTray.exe',
    'Uninstall.ps1',
    'Uninstall.cmd',
    'README.md',
    'README-ar.md',
    'LICENSE.txt',
    'NOTICE.md',
    'PRIVACY.md',
    'SUPPORTED-DEVICES.md',
    'DOTNET-LICENSE.txt',
    'THIRD-PARTY-NOTICES.txt'
)

foreach ($fileName in $filesToInstall) {
    $source = Join-Path $PSScriptRoot $fileName
    if (Test-Path -LiteralPath $source -PathType Leaf) {
        $destination = Join-Path $installFolder $fileName
        $copied = $false
        for ($attempt = 0; $attempt -lt 24; $attempt++) {
            try {
                Copy-Item -LiteralPath $source -Destination $destination -Force -ErrorAction Stop
                $copied = $true
                break
            }
            catch {
                Start-Sleep -Milliseconds 250
            }
        }
        if (-not $copied) {
            throw "Could not update $destination because it remained in use."
        }
    }
}

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
New-ItemProperty -Path $runKey -Name 'RedragonBatteryTray' `
    -Value ('"' + $installedExe + '"') -PropertyType String -Force | Out-Null

$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\RedragonM913BatteryTray'
New-Item -Path $uninstallKey -Force | Out-Null
$uninstallCommand = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' +
    (Join-Path $installFolder 'Uninstall.ps1') + '"'
$estimatedSizeKb = [math]::Ceiling((Get-ChildItem -LiteralPath $installFolder -File |
    Measure-Object -Property Length -Sum).Sum / 1KB)
$uninstallValues = @{
    DisplayName = 'Redragon M913 Battery Tray'
    DisplayVersion = '1.5.1'
    Publisher = 'Independent open-source utility'
    InstallLocation = $installFolder
    DisplayIcon = $installedExe
    UninstallString = $uninstallCommand
    QuietUninstallString = $uninstallCommand
    NoModify = 1
    NoRepair = 1
    EstimatedSize = $estimatedSizeKb
}
foreach ($entry in $uninstallValues.GetEnumerator()) {
    New-ItemProperty -Path $uninstallKey -Name $entry.Key -Value $entry.Value -Force | Out-Null
}

if (-not $NoStart) {
    Start-Process -FilePath $installedExe -WorkingDirectory $installFolder
}

Write-Host 'Installed Redragon M913 Battery Tray 1.5.1 for the current Windows user.'
