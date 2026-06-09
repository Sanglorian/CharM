#requires -Version 7.0
<#
.SYNOPSIS
    Build CharM standalone webserver for Linux (x64 or arm64): self-contained
    single-file deployment.

.DESCRIPTION
    No MAUI workload required — CharM.Web is a pure ASP.NET Blazor Server app.
    Cross-publishes fine from any host (Windows, macOS, Linux) because .NET 10
    ships full self-contained runtime support for linux-x64 and linux-arm64.

    Single-file mode bundles all managed assemblies into one exe;
    IncludeNativeLibrariesForSelfExtract=true ensures native deps (SignalR
    WebSocket libs, Kestrel ICU) are also embedded and extracted on first run
    rather than dynamically loaded — without that flag single-file silently
    fails to start ASP.NET Core apps.

.PARAMETER Configuration
    Debug or Release. Default: Release.

.PARAMETER Arch
    Target architecture: x64 or arm64. Default: x64. arm64 covers both
    Raspberry Pi 5 / Ampere / AWS Graviton class Linux servers.

.PARAMETER NoZip
    Leave artifacts in scripts/out/linux-<arch>/ and skip the final zip.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')] [string]$Configuration = 'Release',
    [ValidateSet('x64','arm64')]     [string]$Arch          = 'x64',
    [switch]$NoZip
)

$ErrorActionPreference = 'Stop'

$rid  = "linux-$Arch"
$repo = (Resolve-Path "$PSScriptRoot/..").Path -replace '\\','/'
$out  = "$repo/scripts/out"
$work = "$out/linux-$Arch"
New-Item -ItemType Directory -Force -Path $work | Out-Null

Write-Host "==> Publishing CharM.Web for $rid (self-contained, single-file)"
dotnet publish "$repo/src/CharM.Web/CharM.Web.csproj" `
    -c $Configuration `
    -r $rid `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:UseSharedCompilation=false `
    -o $work
if ($LASTEXITCODE -ne 0) { throw "Linux publish failed" }

if (-not $NoZip) {
    $zip = "$out/charm-linux-$Arch.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Write-Host "==> Zipping $work -> $zip"
    Compress-Archive -Path "$work/*" -DestinationPath $zip -CompressionLevel Optimal
    $sizeMb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
    Write-Host "==> Built: $zip ($sizeMb MB)"
} else {
    Write-Host "==> Built (unzipped) in: $work"
}
