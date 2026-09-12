[CmdletBinding()]
param(
    [switch]$KeepLogs
)

$ErrorActionPreference = 'Stop'

$localPrograms = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs'))
$installFolder = [IO.Path]::GetFullPath((Join-Path $localPrograms 'RedragonM913BatteryTray'))
$expectedParent = [IO.Path]::GetFullPath($localPrograms).TrimEnd('\') + '\'
if (-not $installFolder.StartsWith($expectedParent, [StringComparison]::OrdinalIgnoreCase) -or
    [IO.Path]::GetFileName($installFolder) -ne 'RedragonM913BatteryTray') {
    throw "Refusing to remove an unexpected path: $installFolder"
}

$installedExe = Join-Path $installFolder 'RedragonBatteryTray.exe'
$running = Get-Process RedragonBatteryTray -ErrorAction SilentlyContinue |
    Where-Object {
        try { $_.Path -eq $installedExe } catch { $false }
    }
if ($running) {
    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 5 -ErrorAction SilentlyContinue
}

Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' `
    -Name 'RedragonBatteryTray' -ErrorAction SilentlyContinue
Remove-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\RedragonM913BatteryTray' `
    -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath 'HKCU:\Software\RedragonM913BatteryTray' `
    -Recurse -Force -ErrorAction SilentlyContinue

if (Test-Path -LiteralPath $installFolder) {
    Remove-Item -LiteralPath $installFolder -Recurse -Force
}

if (-not $KeepLogs) {
    $logFolder = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'RedragonBatteryTray'))
    $expectedLogs = [IO.Path]::GetFullPath($env:LOCALAPPDATA).TrimEnd('\') + '\'
    if ($logFolder.StartsWith($expectedLogs, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($logFolder) -eq 'RedragonBatteryTray' -and
        (Test-Path -LiteralPath $logFolder)) {
        Remove-Item -LiteralPath $logFolder -Recurse -Force
    }
}

Write-Host 'Redragon M913 Battery Tray was removed.'
