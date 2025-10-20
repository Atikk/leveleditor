[CmdletBinding()]
param(
    [string]$OutputRoot,
    [string]$SessionPrefix = (Get-Date -Format "yyyyMMdd-HHmmss"),
    [string[]]$Workers = @("2"),
    [string[]]$Schedulers = @("async", "workstealing", "bifurcated"),
    [string]$Configuration = "Debug",
    [switch]$NoBuild,
    [switch]$DisableHeadless,
    [int]$HeadlessFrames = 600,
    [int]$HeadlessJobsPerFrame = 96,
    [int]$HeadlessJobIterations = 8,
    [int]$HeadlessInnerLoopIterations = 256,
    [int]$HeadlessBatchSize = 1
)

$scriptRoot = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptRoot)) {
    $scriptRoot = Split-Path -LiteralPath $MyInvocation.MyCommand.Path
}

$repoRoot = (Resolve-Path (Join-Path $scriptRoot "..")).ProviderPath

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "telemetry-runs"
} elseif (-not [System.IO.Path]::IsPathRooted($OutputRoot)) {
    $cwd = (Get-Location).ProviderPath
    $OutputRoot = [System.IO.Path]::GetFullPath((Join-Path $cwd $OutputRoot))
}

if ($DisableHeadless) {
    Write-Host "Headless mode disabled; runtime will attempt to create a graphics device." -ForegroundColor Yellow
}

function Resolve-WorkerCounts([string[]]$input) {
    if ($null -eq $input -or $input.Count -eq 0) {
        return @(2)
    }

    $joined = ($input | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join " "
    if ([string]::IsNullOrWhiteSpace($joined)) {
        return @(2)
    }

    $counts = @()
    $segments = $joined -split '[,;\s]'

    foreach ($segment in $segments) {
        if ([string]::IsNullOrWhiteSpace($segment)) {
            continue
        }

        [int]$parsed = 0
        if ([int]::TryParse($segment, [ref]$parsed)) {
            $clamped = [Math]::Max(1, [Math]::Min(64, $parsed))
            if ($counts -notcontains $clamped) {
                $counts += $clamped
            }
        }
    }

    if ($counts.Count -eq 0) {
        return @(2)
    }

    return $counts
}

$projectPath = Join-Path (Join-Path $repoRoot "DotGame.Runtime") "DotGame.Runtime.csproj"

if (-not (Test-Path $OutputRoot)) {
    New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
}

if (-not $NoBuild) {
    Write-Host "Building DotGame.Runtime ($Configuration)..." -ForegroundColor Yellow
    dotnet build $projectPath --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }
}

$workerSummary = if ($Workers) { ($Workers | ForEach-Object { $_ }) -join ', ' } else { '<none>' }
Write-Host "Raw worker arguments: $workerSummary" -ForegroundColor DarkGray

$workerCounts = Resolve-WorkerCounts $Workers
Write-Host "Resolved worker counts: $($workerCounts -join ', ')" -ForegroundColor DarkGray
$results = @();

foreach ($workerCount in $workerCounts) {
    foreach ($scheduler in $Schedulers) {
        $sessionName = "$SessionPrefix-w$workerCount-$scheduler"
        $sessionDir = Join-Path $OutputRoot $sessionName
        if (-not (Test-Path $sessionDir)) {
            New-Item -ItemType Directory -Path $sessionDir -Force | Out-Null
        }

        $env:DOTGAME_QA_TELEMETRY_DIR = $sessionDir
        $env:DOTGAME_QA_SESSION = $sessionName
        $env:DOTGAME_RUNTIME_JOB_SYSTEM = $scheduler
        $env:DOTGAME_RUNTIME_JOB_WORKERS = $workerCount
        if (-not $DisableHeadless) {
            $env:DOTGAME_RUNTIME_HEADLESS = "1"
            $env:DOTGAME_RUNTIME_HEADLESS_FRAMES = $HeadlessFrames
            $env:DOTGAME_RUNTIME_HEADLESS_JOBS = $HeadlessJobsPerFrame
            $env:DOTGAME_RUNTIME_HEADLESS_ITERATIONS = $HeadlessJobIterations
            $env:DOTGAME_RUNTIME_HEADLESS_WORK = $HeadlessInnerLoopIterations
            $env:DOTGAME_RUNTIME_HEADLESS_BATCH = $HeadlessBatchSize
        }
        else {
            $env:DOTGAME_RUNTIME_HEADLESS = $null
            $env:DOTGAME_RUNTIME_HEADLESS_FRAMES = $null
            $env:DOTGAME_RUNTIME_HEADLESS_JOBS = $null
            $env:DOTGAME_RUNTIME_HEADLESS_ITERATIONS = $null
            $env:DOTGAME_RUNTIME_HEADLESS_WORK = $null
            $env:DOTGAME_RUNTIME_HEADLESS_BATCH = $null
        }

        Write-Host "Running runtime with scheduler '$scheduler' (workers=$workerCount)..." -ForegroundColor Cyan
        dotnet run --project $projectPath --configuration $Configuration --no-build
        $exitCode = $LASTEXITCODE

        $results += [pscustomobject]@{
            Scheduler = $scheduler
            Workers   = $workerCount
            Session   = $sessionName
            ExitCode  = $exitCode
            Output    = $sessionDir
        }

        if ($exitCode -ne 0) {
            Write-Warning "Runtime exited with code $exitCode for scheduler '$scheduler' (workers=$workerCount)."
            break
        }
    }

    if ($results.Count -gt 0 -and $results[-1].ExitCode -ne 0) {
        break
    }
}

$env:DOTGAME_QA_TELEMETRY_DIR = $null
$env:DOTGAME_QA_SESSION = $null
$env:DOTGAME_RUNTIME_JOB_SYSTEM = $null
$env:DOTGAME_RUNTIME_JOB_WORKERS = $null
$env:DOTGAME_RUNTIME_HEADLESS = $null
$env:DOTGAME_RUNTIME_HEADLESS_FRAMES = $null
$env:DOTGAME_RUNTIME_HEADLESS_JOBS = $null
$env:DOTGAME_RUNTIME_HEADLESS_ITERATIONS = $null
$env:DOTGAME_RUNTIME_HEADLESS_WORK = $null
$env:DOTGAME_RUNTIME_HEADLESS_BATCH = $null

Write-Host ""
Write-Host "Telemetry sweep complete:" -ForegroundColor Green
$results | Format-Table Scheduler, Workers, Session, ExitCode, Output -AutoSize

return $results
