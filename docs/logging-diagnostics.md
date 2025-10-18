# Logging & Diagnostics Guidance

This document captures the conventions and extension points for the shared logging, assertion, and diagnostic tooling that now powers the editor/runtime stack. Treat it as a living reference; update it as new sinks, helpers, or policies evolve.

## 1. Architecture Snapshot

- **Core types** live under `DotGame.Core/Logging/`:
  - `LogLevel`, `LogEvent`, `ILogger`, `Logger`, `LoggerFactory`, `LogManager`.
  - Sinks: `ConsoleLogSink`, `FileLogSink`, `BufferedLogSink` (UI feed), plus the base `ILogSink` contract.
  - Utilities: `LoggerExtensions.LogException(...)`, `EngineAssert` for invariant enforcement.
- **Bootstrap** entry point is `DotGame/src/App/LoggingBootstrapper.cs`. `Program.Main` calls `LoggingBootstrapper.Initialize()` on startup and `Dispose()` during shutdown.
- **UI integration**: `EditorWindow` subscribes to `BufferedLogSink` snapshots, pushing entries into the Logs tab and updating the alert banner via warning/error counters.

## 2. Usage Guidelines

### 2.1 Obtaining Loggers

```csharp
private static readonly ILogger logger = LogManager.GetLogger<MyType>();
```

- Prefer `GetLogger<T>()` so category names follow CLR type names.
- When cross-cutting concerns warrant it, introduce dedicated categories (e.g. `LogManager.GetLogger("ContentPipeline")`).

### 2.2 Logging Conventions

| Level      | Typical Scenarios                                                                 | UI Severity |
|------------|-------------------------------------------------------------------------------------|-------------|
| `Debug`    | Verbose instrumentation, layout probes, performance probes during dev              | Hidden      |
| `Info`     | User-facing actions, state changes worth tracking in history/logs                  | Informational |
| `Warning`  | Recoverable failures, missing optional data, degraded-mode execution               | Warning banner |
| `Error`    | Non-fatal but significant failures (I/O, serialization issues, runtime preview sync) | Error banner |
| `Critical` | Crashes, asserts, unrecoverable conditions                                         | Error banner |

- Pair user guidance with warnings/errors by also calling `PushHistory(...)` when the operator needs to act.
- Use structured strings (include identifiers, coordinates, resource names) to simplify log search.
- Always attach the exception object when logging errors: `logger.Error("Failed to load map", ex);`

### 2.3 Assertions

- Use `EngineAssert.That(condition, message, logger);` for critical invariants.
- Assertions raise a `LogLevel.Critical` event and throw, ensuring both telemetry and early failure.
- Prefer `EngineAssert.NotNull(...)`/`IsTrue(...)` overloads if more helper variants become available.

### 2.4 Threading & Async Contexts

- `BufferedLogSink` is thread-safe; UI components marshal back to the dispatcher internally.
- When logging inside async continuations, ensure the logger instance is captured once per type to avoid repeated lookups.
- Background workers should keep the sink footprint minimal—emit `Debug/Info` for progress, escalate to `Warning/Error` only when the operator must intervene.

## 3. Configuration & Extensibility

### 3.1 Default Sinks

`LoggingBootstrapper` currently wires:

- `BufferedLogSink` (UI) with size limits to prevent runaway growth.
- `ConsoleLogSink` for developer console visibility.
- Optional file sink (configure `LoggingBootstrapperOptions.LogFilePath`).

### 3.2 Adding New Sinks

1. Implement `ILogSink` and ensure thread-safety.
2. Register inside `LoggingBootstrapper.Initialize()` before `LogManager.MarkInitialized()`.
3. Document rotation/retention strategies for persistent sinks.

### 3.3 Runtime Reconfiguration

- Call `LogManager.SetLogLevelThreshold(...)` to raise/lower verbosity globally.
- `LogManager.GetBufferedSink()` returns the active UI buffer; avoid holding references outside UI lifetimes.

## 4. Editor/UI Responsibilities

- Maintain the warning/error counters when adding or removing log entries so the status banner reflects current health.
- History entries (`PushHistory`) complement logs; reserve them for actionable events instead of verbose traces.
- When a log entry represents a user-visible failure (e.g., map save), mirror the message in history or inline notifications.

## 5. Deterministic Frame Timing Roadmap

To align with the 60/120 FPS determinism goals:

| Milestone                                    | Owner/Notes |
|----------------------------------------------|-------------|
| Instrument `GameClock` with high-resolution timing + drift accumulation | TBD |
| Emit per-frame timing via `logger.Debug` under a dedicated `FrameTiming` category | TBD |
| Surface frame stats in the UI (status bar overlay or diagnostics panel) | TBD |
| Capture periodic frame timing snapshots to file for offline analysis | TBD |

- When the job system lands, integrate task execution stats into the same timing feed.
- Keep deterministic tooling opt-in to avoid noise for casual editor usage.

## 6. Action Items Checklist

- [ ] Add README snippet referencing this document.
- [ ] Define `LoggingBootstrapperOptions` for file path rotation policy.
- [ ] Author coding standards entry for log message style/format.
- [ ] Schedule instrumentation spike for `GameClock` (deterministic loop support).

Maintain this document alongside related subsystems so every contributor understands how diagnostics flow from engine to UI and beyond.
