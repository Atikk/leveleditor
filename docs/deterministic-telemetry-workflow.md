# Deterministic Telemetry Workflow

This guide describes how to capture, aggregate, and analyse runtime telemetry for scheduler comparisons during Part II of the roadmap. It complements the job-system API notes and serves as the reference for both local validation and upcoming CI automation.

## Prerequisites

- .NET SDK 8.0
- PowerShell (Windows) or PowerShell Core
- Python 3.11+ (for the analysis helper)
- Optional: virtual environment or requirements file if integrating into CI agents

Ensure the repository is restored and built once so content assets and tools are ready:

```powershell
cd c:\path\to\leveleditor
 dotnet build leveleditor.sln
```

## Step 1 — Sweep Scheduler Runs

Use `scripts\collect-job-system-telemetry.ps1` to execute deterministic runtime sessions for each scheduler candidate.

```powershell
# Example: capture telemetry under .\telemetry-runs\20251019-qa
./scripts/collect-job-system-telemetry.ps1 `
  -OutputRoot .\telemetry-runs `
  -SessionPrefix 20251019-qa `
  -Workers 2,4
```

### Flags

- `-Schedulers "async","workstealing","bifurcated"` — override the default sweep set.
- `-Workers <int[]>` — provide one or more worker counts (range 1-64). Session folders are suffixed with `w<count>` to distinguish the captures.
- Headless configuration (defaults to the deterministic harness):
  - `-DisableHeadless` — opt out of the headless harness and run the full MonoGame window (requires a GPU/display).
  - `-HeadlessFrames`, `-HeadlessJobsPerFrame`, `-HeadlessJobIterations`, `-HeadlessInnerLoopIterations`, `-HeadlessBatchSize` — tune the deterministic workload. The script exports these values via `DOTGAME_RUNTIME_HEADLESS_*` variables for the runtime to consume.
- `-Configuration Release` — run release builds if profiling final numbers.
- `-RuntimeIdentifier linux-x64` — cross-compile/publish for a specific runtime before running the sweep (use `osx-arm64` on macOS, etc.).
- `-PlatformServices linux` — select the platform services implementation exposed via `DOTGAME_PLATFORM_IMPLEMENTATION`. Values: `windows`, `linux`, `mac` (defaults to `windows`).
- `-NoBuild` — skip the build if the artifacts are already up to date.

Each run produces JSON + CSV exports per session beneath the output directory. The JSON payload now includes `platform` (OS/architecture/runtime uptime), `memory` (working set, private bytes, managed heap), and a `metadata` map capturing resolved configuration (platform identifier, job system selection, worker count, headless options). Environment variables are restored to previous values once the script completes. Publish profile details live in `docs/runtime-publishing.md` if you need prebuilt artifacts instead of `dotnet run`.

The sweep script enables the headless deterministic harness by default (`DOTGAME_RUNTIME_HEADLESS=1`), so no graphics device is required on CI agents. Use `-DisableHeadless` when you need to profile the full MonoGame surface locally.

## Step 2 — Summarise Telemetry

After the sweep, call the Python helper to produce human-readable metrics and (optionally) machine-readable JSON for dashboards.

```powershell
python ./scripts/analyze-job-system-telemetry.py .\telemetry-runs\20251019-qa `
    --json .\telemetry-runs\20251019-qa\summary.json
```

Sample console output:

```
Session: 20251019-qa-w2-async
  meta[platform.resolved] = windows
  meta[jobSystem.resolved] = async
  meta[jobSystem.workers] = 2
  async: samples=3600, pending(avg=1.245, max=9), active(avg=0.742, peak=2), configured=2, completedFinal=10800
Session: 20251019-qa-w4-workstealing
  meta[platform.resolved] = windows
  meta[jobSystem.resolved] = workstealing
  meta[jobSystem.workers] = 4
  workstealing: samples=3600, pending(avg=0.842, max=5), active(avg=1.441, peak=4), configured=4, completedFinal=10800
Session: 20251019-qa-w4-bifurcated
  meta[platform.resolved] = windows
  meta[jobSystem.resolved] = bifurcated
  meta[jobSystem.workers] = 4
  bifurcated: samples=3600, pending(avg=1.014, max=7), active(avg=1.219, peak=3), configured=4, completedFinal=10800
```

The JSON artifact mirrors the structure, enabling a CI job to upload structured telemetry for dashboards.

### Baseline regression checks

To compare a fresh sweep against an established baseline, point the analyzer at a JSON snapshot from a prior known-good run:

```powershell
python ./scripts/analyze-job-system-telemetry.py .\telemetry-runs\20251019-qa `
  --baseline .\ci\telemetry-baseline.json `
  --regression-tolerance 0.05 `
  --fail-on-regression
```

- `--baseline` expects the same structure produced by the `--json` flag (session → job → metrics). Use `ci/telemetry-baseline.json.example` as a template and check in the real baseline file once metrics are captured.
- `--regression-tolerance` defines the allowed fractional drift (e.g., `0.05` permits a 5 % increase in pending backlog metrics before flagging a regression).
- `--fail-on-regression` exits with code `1` if the run exceeds the tolerated bounds.
- For planning the evaluation matrix ahead of the baseline capture, consult `docs/scheduler-evaluation-playbook.md`.
- The runtime background scheduler automatically mirrors the resolved worker count so asset-loading threads stay in sync with the active job system during sweeps.

## CI Integration Checklist

1. **Runner tooling** — install .NET 8.0, PowerShell Core, and Python 3 on the agent image.
2. **Cache** — reuse NuGet and dotnet caches to reduce build time.
3. **Run telemetry sweep** — invoke the PowerShell script with `-NoBuild` when preceded by the build step. The GitHub Actions workflow at `.github/workflows/deterministic-telemetry.yml` demonstrates a reference implementation on `windows-latest` runners and reads worker counts from the `TELEMETRY_WORKERS` environment variable (defaults to `2,4`).
4. **Analyse** — call the Python script and stash the JSON summary as a build artifact. If a baseline file exists at `ci/telemetry-baseline.json`, supply it with `--baseline`/`--fail-on-regression` to gate the build; otherwise the workflow publishes the JSON into the job summary and uploads the telemetry directory for downstream dashboards.
5. **Dashboards** — consume the JSON to populate charts (e.g., GitHub Actions summary, Grafana, etc.).
6. **Fail conditions** — optional gating on thresholds (e.g., if `pendingMax` exceeds baseline).

## Manual Profiling Tips

- Increase the session length by modifying the deterministic harness duration (e.g., run longer fixed step counts) when analysing extended workloads.
- Override the headless harness via environment variables (e.g., `DOTGAME_RUNTIME_HEADLESS_FRAMES=1200`, `DOTGAME_RUNTIME_HEADLESS_JOBS=128`) when you need longer or heavier workloads.
- Use `DOTGAME_QA_SESSION` to tag runs with meaningful identifiers (`CI-PR123`, `nightly`, etc.).
- Combine telemetry exports with frame CSVs for correlation (frame-id alignment enables drift + pending job comparisons).

## Future Work

- Upload analyser output to an online dashboard automatically.
- Extend the CSV schema with queue-depth histograms once the winning scheduler is selected.
- Add editor preview telemetry capture to compare in-editor workloads against standalone runs.
- Expand CI workflow to gate on thresholds (e.g., fail if pending backlog exceeds baseline) once baseline metrics are established.
