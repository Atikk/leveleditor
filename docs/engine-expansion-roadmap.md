# Engine Expansion Roadmap

This roadmap captures the multi-part initiative outlined for the editor/runtime stack. Each "Part" aggregates capabilities, dependencies, and immediate next actions. Use this document to coordinate planning, spike work, and cross-team integrations.

---

## Guiding Core Concepts

- **Deterministic 60/120 FPS Tooling**: Shared timing APIs, frame budgeting, telemetry feeds.
- **Advanced Rendering Abstraction**: Unified device/command-layer that supports multiple pipelines (forward, deferred, compute-driven).
- **Data-Oriented Layout Utilities**: Helper libraries and documentation for cache-friendly structures, archetype scheduling, and introspection.
- **Modular Layering Documentation**: Formalize subsystem boundaries, lifecycle hooks, and extension points beyond current implied structure.

> **Action**: Produce a "Core Concepts Blueprint" before deep-diving Part III onwards so downstream teams align on constraints and instrumentation expectations.

---

## Part II — Runtime Foundation

**Scope:**
- Custom memory allocators (linear/arena, pool, stack, fragmentation telemetry).
- Global logging/assertion/error framework (✅ logging base implemented; continue expansion).
- OS abstraction layer & build system integration (filesystem, threading, timers, windowing).
- Comprehensive job system with scheduling domains, priorities, fiber/work-stealing pools, sync primitives (fences, semaphores, barriers).

**Dependencies:** Logging docs (done), platform interface spec (TBD).

**Next Actions:**
1. Draft `Platform` interface (threads, file I/O, time sources) with platform-specific stubs.
2. Prototype arena allocator with instrumentation hooks feeding logs/telemetry.
3. Design job system API surface, schedule spike comparing work-stealing vs. bifurcated queues.

---

## Part III — Rendering Architecture

**Scope:**
- Graphics pipeline control layer (device abstraction, render graph, resource lifetime management).
- Scene graph & culling (hierarchical visibility, occlusion, lod management).
- Lighting suite (deferred/clustered, shadow cascades, IBL, volumetrics, post-processing stack).
- Configurable render architectures (forward+, deferred, compute-driven paths) with PBR/HDR support and compute queue integration.

**Dependencies:** Part II job system (for async resource loading & render threading), Core Concepts blueprint.

**Next Actions:**
1. Author render device interface doc (buffer, texture, shader resources, command submission).
2. Spike render-graph prototype executing trivial passes via abstraction.
3. Outline scene graph data layout + culling heuristics (include data-oriented guidelines).

---

## Part IV — Animation Systems

**Scope:**
- Skeletal animation runtime (hierarchical pose evaluation, retargeting).
- Blend trees, animation state machines, IK solvers (CCD/FABRIK), additive layers.
- Compression pipeline (curve simplification, keyframe reduction) integrating with content pipeline.
- Motion graphs and gameplay/physics hooks for state feedback.

**Dependencies:** Part II job system (parallel pose evaluation), Part III scene graph (for attached visuals).

**Next Actions:**
1. Define animation clip format + runtime cache interfaces.
2. Spike blend tree evaluator using job system tasks.
3. Document IK extension points for gameplay/physics coupling.

---

## Part V — Physics Framework

**Scope:**
- Rigid-body physics engine: integration methods, contact manifolds, constraints.
- Collision pipeline: broad phase (sweep & prune, BVH), narrow phase (GJK/EPA, SAT).
- Middleware bridging layer for optional third-party engines (Havok/Bullet/PhysX).

**Dependencies:** Part II (allocators, job system), Part IV (animation hooks), Part III (scene updates).

**Next Actions:**
1. Establish physics service boundary + messaging with game runtime.
2. Prototype broad-phase acceleration structure shared with scene graph.
3. Draft middleware adapter interface contract.

---

## Part VI — Gameplay Scripting & AI

**Scope:**
- Embedded scripting VM bindings (Lua, Python) with hot-reload support.
- Event bus + state machine framework beyond current trigger system.
- AI toolkit (behavior trees, planners, blackboard service) integrated with ECS tooling.

**Dependencies:** Part II job system, core logging (for script diagnostics).

**Next Actions:**
1. Evaluate scripting host options; decide on embedding vs. external process.
2. Design event/state-machine API that slots into existing ECS.
3. Spike AI behavior tree executor leveraging job system for async behaviors.

---

## Part VII — Content Pipeline & Asset Management

**Scope:**
- Automated content pipeline with incremental builds, dependency tracking.
- DCC (Digital Content Creation) exporters/importers (FBX/glTF/Blender plugins).
- Asset database with versioning, metadata tagging, and build automation integration.
- Tight editor integration (asset inspector, pipeline status overlays).

**Dependencies:** Part II logging/telemetry, Part III rendering specs, Part IV animation formats.

**Next Actions:**
1. Draft pipeline configuration format (YAML/JSON) describing stages and dependencies.
2. Prototype asset database schema (SQLite or custom registry) with version hashing.
3. Schedule DCC exporter spikes targeting top-priority formats.

---

## Part VIII — Systems Infrastructure

**Scope:**
- Audio engine: 3D spatialization, DSP graph, streaming assets.
- Networking/replication layer: session management, interpolation/prediction, rollback support.
- Profiling/optimization toolkit: CPU/GPU captures, allocator telemetry, log timeline correlation.

**Dependencies:** Part II job system, Part III rendering, Part VII asset pipeline.

**Next Actions:**
1. Define audio graph data structures + job assignments.
2. Draft networking replication model doc (authoritative vs. peer-to-peer support).
3. Plan profiling HUD + capture/export format aligned with determinism tooling.

---

## Part IX — Advanced R&D Modules

**Scope:**
- Data-oriented exemplars (SoA/ECS layout showpieces, case studies).
- Multicore scaling strategies (task graphs, NUMA awareness, async streaming patterns).
- Cloud/distributed runtime experiments (remote simulation, editor collaboration, build farm integration).

**Dependencies:** Completion of Part II-VIII to supply real subsystems for experimentation.

**Next Actions:**
1. Curate topic list for data-oriented modules; align with documentation team.
2. Identify candidate features for cloud/distributed pilot (e.g., remote asset cooking).
3. Draft research backlog with prioritization criteria (impact, feasibility, reuse potential).

---

## Cross-Cutting Documentation & Governance

- **Modular Layering Guide**: produce architecture doc aligning naming, ownership boundaries, and extension hooks across systems.
- **Terminology & Coding Standards**: ensure all new parts adhere to consistent naming/conventions, especially around diagnostics and job system usage.
- **Milestone Tracking**: maintain a living project board referencing this roadmap; update with status, risks, and discovered dependencies.

---

### Immediate Coordination Checklist

- [ ] Publish "Core Concepts Blueprint" covering timing, rendering abstraction, data layout utilities, and modularity docs.
- [ ] Assign leads for Part II subsystems (allocators, job system, platform layer).
- [ ] Schedule rendering architecture kickoff workshop (Part III).
- [ ] Open RFC template for cross-team design proposals referencing roadmap sections.

Keep this roadmap under version control (`docs/engine-expansion-roadmap.md`) and review it during planning meetings so scope changes and completed work remain visible.
