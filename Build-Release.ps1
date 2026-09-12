[CmdletBinding()]
param(
    [string]$OutputRoot = (Join-Path $PSScriptRoot 'release'),
    [string]$DotNetPath = 'dotnet'
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_NOLOGO = '1'
$version = '1.5.1'
$packageName = "Redragon-M913-Battery-Tray-$version-win-x64"
$outputRootFull = [IO.Path]::GetFullPath($OutputRoot)
$staging = [IO.Path]::GetFullPath((Join-Path $outputRootFull $packageName))
$safeRoot = $outputRootFull.TrimEnd('\') + '\'
if (-not $staging.StartsWith($safeRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unexpected staging path: $staging"
}

New-Item -ItemType Directory -Path $outputRootFull -Force | Out-Null
if (Test-Path -LiteralPath $staging) {
    Remove-Item -LiteralPath $staging -Recurse -Force
}
& (Join-Path $PSScriptRoot 'Generate-AppIcon.ps1')
& $DotNetPath publish (Join-Path $PSScriptRoot 'RedragonBatteryTray.csproj') `
    -c Release -r win-x64 --self-contained true -o $staging
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

$releaseFiles = @(
    'Install.ps1', 'Install.cmd', 'Uninstall.ps1', 'Uninstall.cmd',
    'README.md', 'README-ar.md', 'LICENSE.txt', 'NOTICE.md', 'PRIVACY.md',
    'SUPPORTED-DEVICES.md', 'SECURITY.md', 'CHANGELOG.md', 'RELEASE-STATUS.md'
)
foreach ($fileName in $releaseFiles) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $fileName) `
        -Destination (Join-Path $staging $fileName) -Force
}

$resolvedDotNet = (Get-Command $DotNetPath -ErrorAction Stop).Source
$dotnetRoot = Split-Path -Parent $resolvedDotNet
Copy-Item -LiteralPath (Join-Path $dotnetRoot 'LICENSE.txt') `
    -Destination (Join-Path $staging 'DOTNET-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $dotnetRoot 'ThirdPartyNotices.txt') `
    -Destination (Join-Path $staging 'THIRD-PARTY-NOTICES.txt') -Force

$hashFile = Join-Path $staging 'SHA256SUMS.txt'
$hashLines = Get-ChildItem -LiteralPath $staging -File |
    Where-Object Name -ne 'SHA256SUMS.txt' |
    Sort-Object Name |
    ForEach-Object { '{0} *{1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash, $_.Name }
[IO.File]::WriteAllLines($hashFile, $hashLines, [Text.UTF8Encoding]::new($false))

$zipPath = Join-Path $outputRootFull ($packageName + '.zip')
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zipPath -CompressionLevel Optimal -Force
(Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash + ' *' + [IO.Path]::GetFileName($zipPath) |
    Set-Content -LiteralPath (Join-Path $outputRootFull ($packageName + '.zip.sha256')) -Encoding ascii

$sourceName = "Redragon-M913-Battery-Tray-$version-source"
$sourceStaging = [IO.Path]::GetFullPath((Join-Path $outputRootFull $sourceName))
if (-not $sourceStaging.StartsWith($safeRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unexpected source staging path: $sourceStaging"
}
if (Test-Path -LiteralPath $sourceStaging) {
    Remove-Item -LiteralPath $sourceStaging -Recurse -Force
}
New-Item -ItemType Directory -Path $sourceStaging -Force | Out-Null
$sourceFiles = @(
    'AppLog.cs', 'AppText.cs', 'M913BatteryReader.cs', 'NativeTrayIcon.cs',
    'Program.cs', 'TrayApplicationContext.cs', 'RedragonBatteryTray.csproj',
    'app.manifest', 'app.ico', 'global.json', '.gitignore', 'Build-Release.ps1', 'Generate-AppIcon.ps1',
    'Install.ps1', 'Install.cmd', 'Uninstall.ps1', 'Uninstall.cmd',
    'README.md', 'README-ar.md', 'LICENSE.txt', 'NOTICE.md', 'PRIVACY.md',
    'SUPPORTED-DEVICES.md', 'SECURITY.md', 'CHANGELOG.md', 'RELEASE-STATUS.md'
)
foreach ($fileName in $sourceFiles) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $fileName) `
        -Destination (Join-Path $sourceStaging $fileName) -Force
}
$sourceHashFile = Join-Path $sourceStaging 'SOURCE-SHA256SUMS.txt'
$sourceHashLines = Get-ChildItem -LiteralPath $sourceStaging -File |
    Where-Object Name -ne 'SOURCE-SHA256SUMS.txt' |
    Sort-Object Name |
    ForEach-Object { '{0} *{1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash, $_.Name }
[IO.File]::WriteAllLines($sourceHashFile, $sourceHashLines, [Text.UTF8Encoding]::new($false))
$sourceZip = Join-Path $outputRootFull ($sourceName + '.zip')
Compress-Archive -Path (Join-Path $sourceStaging '*') -DestinationPath $sourceZip -CompressionLevel Optimal -Force
(Get-FileHash -LiteralPath $sourceZip -Algorithm SHA256).Hash + ' *' + [IO.Path]::GetFileName($sourceZip) |
    Set-Content -LiteralPath (Join-Path $outputRootFull ($sourceName + '.zip.sha256')) -Encoding ascii

Write-Host "Release created: $zipPath"
Write-Host "Source created: $sourceZip"
