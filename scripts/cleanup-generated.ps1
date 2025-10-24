<#
Cleanup generated build artifacts and telemetry logs for the leveleditor workspace.
Usage:
  .\cleanup-generated.ps1 [-WhatIf] [-RemoveTelemetry] [-Confirm]

This script removes bin/ and obj/ directories, telemetry-editor output under DotGame bin, and top-level build log files.
It is conservative by default and will prompt before deleting. Use -WhatIf to preview.
#>
param(
    [switch]$WhatIf,
    [bool]$RemoveTelemetry = $true,
    [bool]$Confirm = $true
)

$repoRoot = Split-Path -Parent $PSScriptRoot
Write-Host "Repository root: $repoRoot"

$patterns = @(
    "**\bin",
    "**\obj"
)

$items = @()
foreach ($pattern in $patterns) {
    $items += Get-ChildItem -Path $repoRoot -Directory -Recurse -Force -ErrorAction SilentlyContinue -Include $pattern
}

if ($RemoveTelemetry) {
    $telemetryPath = Join-Path $repoRoot "DotGame\bin\Debug\net8.0\telemetry-editor"
    if (Test-Path $telemetryPath) { $items += Get-ChildItem -Path $telemetryPath -Recurse -Force -ErrorAction SilentlyContinue }
}

$logFiles = @("build_log.txt","dotgame_build.txt","out.txt","err.txt")
foreach ($f in $logFiles) {
    $full = Join-Path $repoRoot $f
    if (Test-Path $full) { $items += Get-Item $full }
}

if ($items.Count -eq 0) { Write-Host "Nothing found to remove."; return }

# Show a short preview
Write-Host "Found the following items to remove (preview):" -ForegroundColor Yellow
$items | Select-Object FullName, Mode | ForEach-Object { Write-Host $_.FullName }

if ($WhatIf) { Write-Host "WhatIf: no changes made."; return }

if ($Confirm) {
    $ok = Read-Host "Proceed to delete the items above? (y/N)"
    if ($ok -ne 'y' -and $ok -ne 'Y') { Write-Host "Aborting."; return }
}

foreach ($it in $items) {
    try {
        Remove-Item -LiteralPath $it.FullName -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Removed: $($it.FullName)"
    } catch {
        Write-Host "Failed to remove: $($it.FullName) - $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "Cleanup complete." -ForegroundColor Green
