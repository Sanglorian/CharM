#requires -Version 7.0
<#
.SYNOPSIS
    Build CharM for Windows (x64 or arm64): MAUI app + standalone webserver,
    both self-contained (NOT single-file — single-file is incompatible with
    Blazor MAUI Hybrid).

.DESCRIPTION
    Publishes:
      - CharM.Maui (net10.0-windows10.0.19041.0, win-<arch>, self-contained)
      - CharM.Web  (net10.0, win-<arch>, self-contained)

    Bundles both into scripts/out/charm-windows-<arch>.zip with two top-level
    subdirectories:
      maui-app/    - the Blazor Hybrid desktop app
      webserver/   - the standalone Blazor Server webserver exe

    Cross-publish from an x64 host to win-arm64 works because the .NET 10
    SDK + maui-windows workload ship arm64 runtime packs. The build is fully
    managed (no native compile) so no arm64 toolchain is required on the host.

    After publish, prunes WindowsAppSDK locale .dll.mui resource folders
    (~70 locales) down to en-us. The app is English-only; non-English .mui
    files only affect OS-rendered system dialog labels for users on
    non-English Windows, which already fall back to English fine.

.PARAMETER Configuration
    Debug or Release. Default: Release.

.PARAMETER Arch
    Target architecture: x64 or arm64. Default: x64.

.PARAMETER SkipMaui
    Skip the MAUI app publish (build only the webserver).

.PARAMETER SkipWebserver
    Skip the webserver publish (build only the MAUI app).

.PARAMETER NoZip
    Leave artifacts in scripts/out/windows-<arch>/{maui-app,webserver} and
    skip the final zip.

.PARAMETER KeepAllLocales
    Skip the post-publish .dll.mui locale pruning step. Use only if you
    actually need a non-English system-dialog UX in the shipped binary.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')] [string]$Configuration = 'Release',
    [ValidateSet('x64','arm64')]     [string]$Arch          = 'x64',
    [switch]$SkipMaui,
    [switch]$SkipWebserver,
    [switch]$NoZip,
    [switch]$KeepAllLocales
)

$ErrorActionPreference = 'Stop'

$rid  = "win-$Arch"
$repo = (Resolve-Path "$PSScriptRoot/..").Path -replace '\\','/'
$out  = "$repo/scripts/out"
$work = "$out/windows-$Arch"
New-Item -ItemType Directory -Force -Path $work | Out-Null

# Workload check (skip install if already present — `dotnet workload install`
# needs admin on Windows because the SDK lives in Program Files)
if (-not $SkipMaui) {
    $hasWorkload = (& dotnet workload list 2>$null) -match '^\s*maui-windows\s'
    if (-not $hasWorkload) {
        Write-Error @"
The 'maui-windows' workload is not installed. Run once from an elevated
PowerShell:
    dotnet workload install maui-windows
or use the helper:
    Start-Process pwsh -Verb RunAs -ArgumentList '-File','$PSScriptRoot/install-workloads.ps1','-Workloads','maui-windows'
"@
        exit 1
    }
}

if (-not $SkipMaui) {
    Write-Host "==> Publishing CharM.Maui ($rid, self-contained, NOT single-file)"
    dotnet publish "$repo/src/CharM.Maui/CharM.Maui.csproj" `
        -f net10.0-windows10.0.19041.0 `
        -c $Configuration `
        -r $rid `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:WindowsPackageType=None `
        -p:WindowsAppSDKSelfContained=true `
        -p:UseSharedCompilation=false `
        -o "$work/maui-app"
    if ($LASTEXITCODE -ne 0) { throw "MAUI publish failed" }
}

if (-not $SkipWebserver) {
    Write-Host "==> Publishing CharM.Web ($rid, self-contained, NOT single-file)"
    dotnet publish "$repo/src/CharM.Web/CharM.Web.csproj" `
        -c $Configuration `
        -r $rid `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:UseSharedCompilation=false `
        -o "$work/webserver"
    if ($LASTEXITCODE -ne 0) { throw "Webserver publish failed" }
}

# Prune WindowsAppSDK / WinUI per-locale .dll.mui resource folders. These are
# copied directly from the WindowsAppSDK runtime pack and there is no MSBuild
# knob for unpackaged (WindowsPackageType=None) apps to filter them at publish
# time. Each folder is small (~40KB) but there are ~70 of them; together they
# clutter the publish layout and inflate the zip noticeably.
#
# Detection: a locale folder is one whose entire content is *.dll.mui (the
# Windows resource format) and whose name parses as a BCP-47 tag. Avoid
# deleting non-locale Microsoft.UI.Xaml asset folders (e.g. "Microsoft.UI.Xaml").
if (-not $SkipMaui -and -not $KeepAllLocales) {
    $mauiOut = "$work/maui-app"
    if (Test-Path $mauiOut) {
        $keep = @('en-us')
        $localePattern = '^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8}){0,3}$'
        $localeDirs = Get-ChildItem $mauiOut -Directory | Where-Object {
            $_.Name -match $localePattern -and ($keep -notcontains $_.Name.ToLowerInvariant())
        } | Where-Object {
            # Only treat it as a locale folder if every file is a .dll.mui
            $files = Get-ChildItem $_.FullName -File -Recurse -ErrorAction SilentlyContinue
            $files -and -not ($files | Where-Object { $_.Extension -ne '.mui' })
        }
        if ($localeDirs) {
            Write-Host "==> Pruning $($localeDirs.Count) WindowsAppSDK locale folder(s) (keeping: $($keep -join ', '))"
            $localeDirs | Remove-Item -Recurse -Force
        }
    }
}

if (-not $NoZip) {
    $zip = "$out/charm-windows-$Arch.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Write-Host "==> Zipping $work -> $zip"
    Compress-Archive -Path "$work/*" -DestinationPath $zip -CompressionLevel Optimal
    $sizeMb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
    Write-Host "==> Built: $zip ($sizeMb MB)"
} else {
    Write-Host "==> Built (unzipped) in: $work"
}
