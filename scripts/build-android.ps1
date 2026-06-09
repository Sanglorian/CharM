#requires -Version 7.0
<#
.SYNOPSIS
    Build CharM as an Android APK (unsigned debug-keystore build).

.DESCRIPTION
    Publishes CharM.Maui for net10.0-android and copies the resulting APK to
    scripts/out/charm-android.apk.

    The APK ships with the default Android debug keystore — fine for testing
    and side-loading, NOT for Play Store distribution. A signed release APK /
    AAB requires a separate signing pipeline.

.PARAMETER Configuration
    Debug or Release. Default: Release.

.PARAMETER JavaSdkDir
    Optional path to the JDK installation MSBuild should use. Forwarded to
    dotnet publish as -p:JavaSdkDirectory. Leave empty to let the Android
    workload auto-detect. Useful locally when the workload's bundled JDK
    isn't present.

.PARAMETER AndroidSdkDir
    Optional path to the Android SDK root. Forwarded as
    -p:AndroidSdkDirectory. Leave empty to let the workload auto-detect.
    GitHub Actions windows-latest has it at C:\Android\android-sdk.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')] [string]$Configuration = 'Release',
    [string]$JavaSdkDir = '',
    [string]$AndroidSdkDir = ''
)

$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path "$PSScriptRoot/..").Path -replace '\\','/'
$out  = "$repo/scripts/out"
New-Item -ItemType Directory -Force -Path $out | Out-Null

# Workload check (skip install if already present — `dotnet workload install`
# needs admin on Windows because the SDK lives in Program Files)
$hasWorkload = (& dotnet workload list 2>$null) -match '^\s*maui-android\s'
if (-not $hasWorkload) {
    Write-Error @"
The 'maui-android' workload is not installed. Run once from an elevated
PowerShell:
    dotnet workload install maui-android
or use the helper:
    Start-Process pwsh -Verb RunAs -ArgumentList '-File','$PSScriptRoot/install-workloads.ps1','-Workloads','maui-android'
"@
    exit 1
}

Write-Host "==> Publishing CharM.Maui for net10.0-android"

# Build the dotnet publish argv as an array so that optional SDK-path
# overrides are passed as individual arguments. (String concatenation
# into a native command passes the whole thing as one argv element with
# embedded quotes and spaces, which dotnet/MSBuild won't parse.)
$publishArgs = @(
    "$repo/src/CharM.Maui/CharM.Maui.csproj"
    '-f', 'net10.0-android'
    '-c', $Configuration
    '-p:EnableAndroidTarget=true'
    '-p:AndroidPackageFormat=apk'
    '-p:UseSharedCompilation=false'
)
if (-not [string]::IsNullOrWhiteSpace($JavaSdkDir)) {
    $publishArgs += "-p:JavaSdkDirectory=$JavaSdkDir"
}
if (-not [string]::IsNullOrWhiteSpace($AndroidSdkDir)) {
    $publishArgs += "-p:AndroidSdkDirectory=$AndroidSdkDir"
}

dotnet publish @publishArgs
if ($LASTEXITCODE -ne 0) { throw "Android publish failed" }

Write-Host "==> Locating APK"
$publishRoot = "$repo/src/CharM.Maui/bin/$Configuration/net10.0-android"
# Prefer signed APK (com.charm.app-Signed.apk) if produced; fall back to any APK
$apk = Get-ChildItem $publishRoot -Recurse -Filter '*-Signed.apk' -File `
    | Sort-Object LastWriteTime -Descending `
    | Select-Object -First 1
if (-not $apk) {
    $apk = Get-ChildItem $publishRoot -Recurse -Filter '*.apk' -File `
        | Sort-Object LastWriteTime -Descending `
        | Select-Object -First 1
}
if (-not $apk) {
    throw "No APK found under $publishRoot — Android publish may have produced an unexpected layout"
}
Write-Host "    Found: $($apk.FullName)"

$dest = "$out/charm-android.apk"
Copy-Item $apk.FullName $dest -Force

$sizeMb = [math]::Round((Get-Item $dest).Length / 1MB, 1)
Write-Host "==> Built: $dest ($sizeMb MB)"
