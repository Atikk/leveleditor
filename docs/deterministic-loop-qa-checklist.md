# Deterministic Loop QA Checklist

Use this checklist to validate 60 Hz and 120 Hz deterministic timing. Update as tooling evolves.

## 1. Test Matrix

| Platform | GPU Driver | Display Refresh | VSync | Notes |
|----------|------------|-----------------|-------|-------|
| Windows 11 | Latest WHQL | 60 Hz | On/Off | Baseline |
| Windows 11 | Latest WHQL | 120 Hz | On/Off | High refresh |
| Windows 11 | Optimus/Hybrid | 60/120 Hz | On/Off | Laptop switching |
| Windows 11 | External Display | 60/120 Hz | On/Off | USB-C/HDMI |

_Add rows per platform as build support expands._

## 2. Pre-Test Setup

- [ ] Enable `FrameTimingLogListener` at `LogLevel.Debug` for aggregation and `LogLevel.Warning` on budget overruns.
- [ ] Attach status widget in editor preview (once UI hook is available) and ensure graph updates live.
- [ ] Reset telemetry buffers (`BufferedLogSink`) to avoid historical noise.

## 3. Test Cases

### 3.1 Baseline 60 Hz Loop
- [ ] Launch editor preview; lock loop to 60 Hz.
- [ ] Run for 10 minutes idle. Expect negligible drift (< 0.5 ms) and zero budget overruns.
- [ ] Interact with editor (tile painting, character placement) for 5 minutes. Verify overrun count stays < 5 per 5-minute window.

### 3.2 Baseline 120 Hz Loop
- [ ] Repeat baseline tests at 120 Hz. Acceptable drift threshold < 1 ms.
- [ ] Document CPU/GPU utilization; note if sustained load triggers fallback to 60 Hz (if implemented).

### 3.3 Stress Scenarios
- [ ] Run asset streaming or background resource loading while loop is locked to 60 Hz. Confirm `FrameTimingLogListener` reports aggregated averages within +/- 10% of target.
- [ ] Enable runtime preview with MonoGame integration; check if fallback to high precision sleep maintains budgets.
- [ ] Force window resize/drag operations; monitor drift spikes and record them.

### 3.4 Hardware Variability
- [ ] Test on integrated GPU hardware (e.g., Intel Iris). Log any sustained overruns.
- [ ] Repeat on high-refresh external monitor to catch vsync mismatch.
- [ ] Capture data on low-power device (battery mode) to evaluate scheduler impact.

## 4. Telemetry Capture

- [ ] Set `DOTGAME_QA_TELEMETRY_DIR` (and optional `DOTGAME_QA_SESSION`) so the runtime harness exports `RuntimeTelemetryRecorder` JSON/CSV traces automatically.
- [ ] Annotate log files with build hash, hardware info, and test scenario.
- [ ] Upload logs to central diagnostics store.

## 5. Pass/Fail Criteria

- No more than 0.1% frames exceeding budget under baseline conditions.
- Drift remains below 2 ms after 1 hour continuous operation at both 60 Hz and 120 Hz.
- Stress scenarios may exceed but must report via logs; create bug if drift > 5 ms sustained or runaway accumulates.

## 6. Reporting

- File issues with log excerpts + hardware details when thresholds are violated.
- Update this doc with new scenarios or findings (e.g., driver-specific quirks).
- Coordinate with engineering to tune sleep thresholds or job scheduling when anomalies are found.
