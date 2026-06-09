#requires -Version 7.0
<#
.SYNOPSIS
    One-time installer for MAUI workloads. Needs admin on Windows
    (because the SDK lives in Program Files); needs sudo on macOS/Linux
    only if dotnet was installed system-wide (Homebrew dotnet is per-user
    so no sudo needed there).

.DESCRIPTION
    Run this once per dev machine BEFORE invoking the per-platform build
    scripts. The build scripts deliberately do not auto-install workloads
    so that they never trigger UAC mid-build.

    Defaults to installing all three MAUI workloads needed by this repo
    (maui-windows, maui-maccatalyst, maui-android). Pass -Workloads to
    install a subset.

.PARAMETER Workloads
    The workload IDs to install. Default: maui-windows, maui-maccatalyst,
    maui-android.

.EXAMPLE
    # Windows (run from elevated PowerShell):
    pwsh -File scripts/install-workloads.ps1

.EXAMPLE
    # Windows — elevate from a normal shell:
    Start-Process pwsh -Verb RunAs -ArgumentList '-File','scripts/install-workloads.ps1'

.EXAMPLE
    # macOS (sudo only if your dotnet install is system-wide):
    sudo pwsh -File scripts/install-workloads.ps1 -Workloads maui-maccatalyst

.EXAMPLE
    # Install just one workload:
    pwsh -File scripts/install-workloads.ps1 -Workloads maui-windows
#>
[CmdletBinding()]
param(
    [string[]]$Workloads = @('maui-windows', 'maui-maccatalyst', 'maui-android')
)

$ErrorActionPreference = 'Stop'

# Skip workloads that wouldn't make sense on the current platform
$filtered = @()
foreach ($w in $Workloads) {
    switch ($w) {
        'maui-windows'     { if ($IsWindows -or ($null -eq $IsWindows)) { $filtered += $w } else { Write-Host "Skipping $w (Windows-only)" } }
        'maui-maccatalyst' { if ($IsMacOS) { $filtered += $w } else { Write-Host "Skipping $w (macOS-only)" } }
        'maui-android'     { $filtered += $w }
        default            { $filtered += $w }
    }
}

if (-not $filtered) {
    Write-Host "No applicable workloads to install on this platform."
    exit 0
}

Write-Host "==> Installing workloads: $($filtered -join ', ')"
dotnet workload install @filtered --skip-manifest-update
if ($LASTEXITCODE -ne 0) {
    Write-Error "Workload install failed. On Windows, run from an elevated PowerShell."
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "==> Installed. Current workloads:"
dotnet workload list
