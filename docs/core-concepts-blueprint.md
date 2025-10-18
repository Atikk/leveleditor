# Core Concepts Blueprint

> Living specification for the technical pillars that underpin Parts II–IX of the engine expansion roadmap. Treat each section as a contract: implementations and design proposals should map back here or propose revisions via RFC.

---

## 1. Deterministic 60/120 FPS Tooling

### 1.1 Goals
- Guarantee stable 60 Hz and 120 Hz update loops for gameplay/editor preview.
- Provide drift monitoring plus correction reporting (log + UI).
- Expose timing telemetry for profiling, replay capture, and performance regression alerts.

### 1.2 Interfaces
- `ITimeSource` abstraction with high-resolution `Now`/`TickFrequency` and monotonic guarantees.
- `FrameLoopController` orchestrating fixed-update and render cadence (configurable targets, fractional remainder handling).
- `IFrameBudgetListener` event hooks: `OnFrameStart`, `OnBudgetExceeded`, `OnFrameEnd` with timing payloads.

### 1.3 Instrumentation & Telemetry
- Integrate with `LogManager` under category `FrameTiming` (default `Debug`, escalate to `Warning` when budgets are exceeded).
- Emit aggregated stats every N frames via buffered sink (min/max/avg durations, overrun counts).
- Provide an opt-in capture mode writing JSON/CSV for offline analysis.

### 1.4 UI Surface
- Editor status widget showing current FPS, frame time, and overrun indicator.
- Detailed diagnostics panel (historical graph, worst offender list, link to profiling capture).
- Toggle to lock preview to 30/60/120 Hz for validation.

### 1.5 Action Items
1. Implement `ITimeSource` (platform layer) with fallback to .NET high-resolution timers.
2. Prototype `FrameLoopController` with deterministic accumulation and jitter smoothing.
3. Hook telemetry into existing logging UI and add status widget mockup.
4. Draft QA checklist for determinism validation (run loops on different hardware, vsync on/off).

---

## 2. Advanced Rendering Abstraction Layer

### 2.1 Goals
- Unify rendering backend interactions (DirectX, Vulkan, OpenGL, Metal) behind a single API surface.
- Support render-graph orchestration, multi-threaded command recording, and resource lifetime tracking.
- Allow multiple pipelines (forward, deferred, compute) to co-exist and share resources without backend rewrites.

### 2.2 Core Interfaces
- `IRenderDevice`: device/adapter selection, resource creation (`CreateBuffer`, `CreateTexture`, `CreatePipelineState`).
- `ICommandQueue` + `ICommandList`: submission model with explicit synchronization (`Fence`, `Semaphore`).
- `RenderGraph` descriptors: node definitions (inputs/outputs), automatic transient resource allocation.
- Resource descriptors (buffers, textures) with explicit usage flags, residency hints, staging policies.

### 2.3 Threading & Jobs
- Command recording designed for integration with job system tasks (Part II).
- Guidelines for thread ownership (per-queue worker pools, fence integration).
- Background streaming pipeline (async uploads) using dedicated copy queue when available.

### 2.4 Extensibility
- Backend registry pattern (e.g., `RenderBackendLoader` discovers directx/vulkan modules at runtime).
- Optional feature layers: ray tracing, mesh shaders, bindless resources (gated via capability flags).
- Configuration schema describing pipeline selection (forward+, deferred, compute-driven) per project/profile.

### 2.5 Action Items
1. Draft interface definitions with sample pseudo-code illustrating render-graph execution.
2. Create comparison matrix of existing backend libraries vs. rolling custom thin layer.
3. Schedule spike: implement minimal pass (clear + blit) using abstraction to validate ergonomics.
4. Document lifecycle rules (init, hot reload, shutdown) and wiring into editor/runtime hosts.

---

## 3. Data-Oriented Layout Utilities

### 3.1 Goals
- Provide reusable utilities for SoA/AoS conversions, cache-friendly iteration, and archetype management.
- Enable developers to reason about memory access patterns, prefetching, and parallel iteration.
- Supply diagnostics for layout validation (cache misses, branch mispredictions, hot-path profiling).

### 3.2 Tooling Components
- `StructLayoutBuilder`: compile-time or code-gen helper for SoA views with random-access handles.
- `ChunkAllocator`: contiguous block allocator with archetype metadata (ties into custom allocators in Part II).
- Iteration helpers: `ForEachHotLoop`, `SIMDSpan`, `BatchIterator` wrappers optimized for CPU cache lines.
- Instrumentation hooks: macros or attributes to log layout stats (size, alignment, fragmentation) into diagnostics pipeline.

### 3.3 Documentation & Examples
- Cookbook covering conversions from existing ECS components to SoA forms.
- Sample profiling sessions (before/after) illustrating performance gains.
- Guidelines on when to choose AoS vs. SoA vs. hybrid, and how to integrate with serialization/networking.

### 3.4 Action Items
1. Inventory current ECS/component layouts and identify top candidates for refactoring.
2. Draft API proposals for `StructLayoutBuilder` and `ChunkAllocator` with example code.
3. Produce tutorial series (docs + sample project) demonstrating layout transforms and profiling outcomes.
4. Integrate layout telemetry into profiling toolkit (Part VIII dependency).

---

## 4. Modular Layering & Documentation Framework

### 4.1 Goals
- Make subsystem boundaries, dependencies, and lifecycle sequences explicit.
- Ensure new modules document extension points and integration contracts from the outset.
- Provide governance guidelines for RFCs, ownership, and code reviews spanning multiple layers.

### 4.2 Deliverables
- **Architecture Map**: visual + textual overview of modules (runtime, rendering, animation, physics, tooling) and their flows.
- **Lifecycle Playbooks**: startup/shutdown sequences, hot reload flows, error recovery paths.
- **Module Template**: README skeleton describing responsibilities, dependencies, public APIs, and logging/assertion policies.
- **RFC Process**: lightweight template and review loop for proposing new subsystems or major changes.

### 4.3 Integration with Tooling
- Tie module descriptors into build system metadata (Part II) to enable targeted builds/tests.
- Hook documentation summaries into the editor (module inspector) for developer onboarding.
- Maintain links between roadmap entries and module documentation for traceability.

### 4.4 Action Items
1. Assemble cross-team workshop to enumerate current implicit layering and pain points.
2. Draft architecture map using existing diagrams (update as modules mature).
3. Publish module README template and require it for all new subsystems in Parts II–VIII.
4. Stand up RFC repository/issue template referencing this blueprint.

---

## 5. Governance & Maintenance

- Update this blueprint whenever contracts change; flag breaking revisions in release notes.
- Cross-reference with `docs/engine-expansion-roadmap.md` (each part should map to a section here).
- Review quarterly to align with delivered features and adjust priorities.

**Immediate Next Steps**
1. Socialize blueprint with leads for Parts II & III; gather feedback.
2. Log follow-up tasks in the project tracker (timing implementation spike, render abstraction RFC, etc.).
3. Link blueprint from roadmap and developer onboarding docs.
