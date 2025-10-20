$repoRoot = Join-Path $PSScriptRoot '..'
Set-Location $repoRoot

Write-Host "Running from $repoRoot"

$env:DOTGAME_RUNTIME_HEADLESS = '1'
$env:DOTGAME_RUNTIME_HEADLESS_FRAMES = '10'
$env:DOTGAME_RUNTIME_HEADLESS_JOBS = '16'
$env:DOTGAME_RUNTIME_HEADLESS_ITERATIONS = '4'
$env:DOTGAME_RUNTIME_HEADLESS_WORK = '128'
$env:DOTGAME_RUNTIME_HEADLESS_BATCH = '2'
$env:DOTGAME_RUNTIME_HEADLESS_FPS = '60'
$env:DOTGAME_RUNTIME_HEADLESS_SAMPLE_STATS = '1'
$env:DOTGAME_RUNTIME_HEADLESS_EXPORT_DIRECTORY = 'telemetry-runs\tmp-headless'
$env:DOTGAME_QA_TELEMETRY_DIR = 'telemetry-runs\tmp-headless'
$env:DOTGAME_QA_SESSION = 'headless-test'

Write-Host 'Configured headless environment variables.'

Remove-Item -LiteralPath $env:DOTGAME_RUNTIME_HEADLESS_EXPORT_DIRECTORY -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $env:DOTGAME_RUNTIME_HEADLESS_EXPORT_DIRECTORY | Out-Null

Write-Host "Created export directory at $env:DOTGAME_RUNTIME_HEADLESS_EXPORT_DIRECTORY"

$dotnetArgs = @('--project', 'DotGame.Runtime\DotGame.Runtime.csproj', '--no-build')

$stdoutPath = Join-Path $env:DOTGAME_RUNTIME_HEADLESS_EXPORT_DIRECTORY 'stdout.log'
$stderrPath = Join-Path $env:DOTGAME_RUNTIME_HEADLESS_EXPORT_DIRECTORY 'stderr.log'

Write-Host 'Launching dotnet process...'
$argumentList = @('run') + $dotnetArgs
$process = Start-Process -FilePath 'dotnet' -ArgumentList $argumentList -NoNewWindow -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru

if ($process -eq $null) {
    Write-Host 'Failed to start dotnet process.'
    exit 1
}

Write-Host "dotnet process id: $($process.Id)"

Wait-Process -Id $process.Id
$process.WaitForExit()
$process.Refresh()
[int]$exitCode = $process.ExitCode
Write-Host ('dotnet run exited with code {0}.' -f $exitCode)
