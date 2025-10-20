# Scheduler Evaluation Playbook

Use this playbook to drive the "select the baseline architecture" milestone in Part II of the roadmap. It outlines the scenarios to capture, the metrics to compare, and a recommended template for reporting decisions.

## 1. Capture Matrix

Run the deterministic telemetry sweep across the scenarios below. Each matrix entry should produce JSON/CSV telemetry plus the analyzer summary.

| Scenario | DOTGAME_RUNTIME_JOB_SYSTEM | Workers | Notes |
|----------|----------------------------|---------|-------|
| Baseline async | `async` | 2 | Matches legacy configuration. |
| Baseline async | `async` | 4 | Mirrors current CI default to validate scaling. |
| Work stealing | `workstealing` | 2 | Validate small worker pool behaviour. |
| Work stealing | `workstealing` | 4 | Compare directly against async baseline. |
| Bifurcated | `bifurcated` | 2 | Ensure hybrid queue cost stays reasonable. |
| Bifurcated | `bifurcated` | 4 | Primary comparison target. |

Add additional rows for higher worker counts or scenario-specific loads once the deterministic harness supports them.

Execute the runs via:

```powershell
./scripts/collect-job-system-telemetry.ps1 `
    -OutputRoot .\telemetry-runs `
    -SessionPrefix 20251019-eval `
    -Workers 2,4
```

Add or remove worker counts by adjusting the `-Workers` list (e.g., `-Workers 2,4,6`). The script suffixes session names with `w<count>` so multiple pools coexist under the same root directory. Headless execution is enabled by default (`DOTGAME_RUNTIME_HEADLESS=1`), ensuring CI agents without GPUs can participate. Use `-DisableHeadless` when you need to validate the full MonoGame renderer locally.

### Headless workload tuning

The deterministic harness accepts several knobs exposed through the sweep script:

- `-HeadlessFrames` — number of fixed-step frames to simulate (default 600).
- `-HeadlessJobsPerFrame` — jobs scheduled each frame.
- `-HeadlessJobIterations` — iterations per job batch descriptor.
- `-HeadlessInnerLoopIterations` — CPU-heavy inner loop per job iteration.
- `-HeadlessBatchSize` — batch size used when scheduling work.

Each flag propagates to the runtime via `DOTGAME_RUNTIME_HEADLESS_*` environment variables so manual runs can mirror the CI configuration.

## 2. Analyse & Record

Use the analyzer to generate summaries for the entire run directory, emit JSON, and compare against an optional baseline if one already exists.

```powershell
python ./scripts/analyze-job-system-telemetry.py .\telemetry-runs\20251019-eval `
    --json .\telemetry-runs\20251019-eval\summary.json `
    --baseline .\ci\telemetry-baseline.json `
    --regression-tolerance 0.05
```

Copy the console output into a working note (Markdown or spreadsheet) so snapshot metrics are easy to compare.

## 3. Qualitative Checks

For each scheduler, track the following observations alongside the numeric telemetry:

- **Frame drift stability** — review frame CSV exports to ensure timing jitter does not spike when job queues fill.
- **Allocator pressure** — confirm arena allocator telemetry matches expectations (no sudden spikes tied to scheduler changes).
- **Completion throughput** — correlate `completedFinal` with deterministic frame counts to ensure no backlog/lost work exists.
- **Worker utilisation** — note whether worker peaks plateau (e.g., work stealing hitting max workers consistently may indicate better load balancing).

## 4. Decision Template

When finalising the baseline selection, write the decision using this structure and commit it under `docs/` (suggested name `scheduler-evaluation-<date>.md`):

1. **Summary** — chosen scheduler/worker configuration and rationale.
2. **Telemetry highlights** — key metrics (pending average/max, active peaks, completion counts) with references to summary JSON.
3. **Risks** — open concerns (e.g., allocator spikes under specific loads, high pending backlog at worker=2).
4. **Follow-up tasks** — implementation or experimentation items before rolling out broadly.

## 5. Updating CI Baselines

After selecting the baseline configuration:

1. Copy the relevant JSON summary section into a new `ci/telemetry-baseline.json` (use `ci/telemetry-baseline.json.example` as the template).
2. Commit the baseline so GitHub Actions enforces regression gating automatically via `--fail-on-regression`.
3. Record the baseline capture date in the evaluation report for traceability.

## 6. Future Enhancements

- Expand the deterministic harness to emulate mixed workloads (AI, streaming) for stress testing once they exist.
- Add automated charts (e.g., line graphs of pending backlog over time) using the JSON summary as input.
- Capture additional metrics such as queue depth histograms or per-domain breakdown when the job system supports it.

Refer back to `docs/deterministic-telemetry-workflow.md` for the step-by-step tooling instructions and to the roadmap for outstanding integration tasks.
