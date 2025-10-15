<#
.SYNOPSIS
  Build and run the Avalonia demo on Windows (helper script).

.DESCRIPTION
  This script builds and runs the `DotGame\DotGameAvalonia.csproj` project
  using the `dotnet` CLI. It performs a small set of sanity checks and
  exposes a few parameters for convenience.

.PARAMETER Configuration
  Build configuration to use (Debug or Release). Default: Debug.

.PARAMETER SkipBuild
  If present, skip the build step and only run the app.

.PARAMETER Detach
  If present, run the application detached (start-process) so the script exits
  while the app keeps running.

.EXAMPLE
  .\run-windows.ps1

.EXAMPLE
  .\run-windows.ps1 -Configuration Release -Detach

#>

param(
    [string]$Configuration = 'Debug',
    [switch]$SkipBuild,
    [switch]$Detach
)

function Write-Info($msg) { Write-Host "[INFO] $msg" -ForegroundColor Cyan }
function Write-Err($msg)  { Write-Host "[ERROR] $msg" -ForegroundColor Red }

Set-StrictMode -Version Latest

$projectPath = Join-Path -Path $PSScriptRoot -ChildPath 'DotGame\DotGameAvalonia.csproj'

Write-Info "Script root: $PSScriptRoot"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Err "dotnet CLI was not found on PATH. Install .NET 8 SDK: https://dotnet.microsoft.com/"
    exit 2
}

if (-not (Test-Path $projectPath)) {
    Write-Err "Project file not found: $projectPath"
    exit 3
}

if (-not $SkipBuild) {
    Write-Info "Building project ($Configuration)..."
    $build = dotnet build $projectPath -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        Write-Err "Build failed. See output above."
        exit $LASTEXITCODE
    }
}

$runInfo = "Running project ($Configuration)"
if ($Detach) { $runInfo += ' (detached)' }
Write-Info $runInfo

if ($Detach) {
    Start-Process -FilePath 'dotnet' -ArgumentList "run --project `"$projectPath`" -c $Configuration" -NoNewWindow
    Write-Info "Started detached."
    exit 0
} else {
    & dotnet run --project "$projectPath" -c $Configuration
    exit $LASTEXITCODE
}
