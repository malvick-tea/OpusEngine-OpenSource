# Contributors

This file summarizes the main contribution areas reflected in the project commit
history. It is a practical attribution note, not a complete changelog.

## idktea

idktea imported the extracted Opus engine workspace and implemented many of the
engine hardening, diagnostics, physics, renderer, and content-rendering changes
that shaped the first-alpha codebase.

Main contribution areas:

- Imported the Opus engine baseline at the M11.6 milestone, including repository
  bootstrap files, artifact ignores, EOL rules, and the verified build/test
  state.
- Added and hardened asynchronous rolling-log writes, including backpressure
  policy, durable flush behavior, bounded shutdown, dispose-safe metrics, and
  crash-safe worker handling.
- Added typed engine failure classification for device loss and content-load
  boundaries, then threaded those classifications into diagnostics and host
  failure reporting.
- Completed consumer telemetry plumbing into overlay panels and failure-report
  lines, and updated host comments once that seam was closed.
- Added live host controls such as the overlay toggle path and resizable D3D12
  live-window handling.
- Added terrain and ground-vehicle physics helpers: off-throttle braking, slope
  coupling, footprint sampling, contact-patch traction, recoil helpers, prop
  resistance, and vehicle-neutral physics fixtures.
- Moved game-domain save shapes out of engine persistence, leaving generic save
  framing and codec behavior in the engine.
- Extended glTF material parsing and renderer planning for full metal-roughness
  material data.
- Implemented the D3D12 PBR forward path, material atlas expansion, external
  material loading, and BC7/BC5 compressed texture caching/upload.
- Added engine support for articulated scenes and relative mouse mode.
- Kept the codebase aligned with local style rules and documentation hygiene.

## VellumYu

VellumYu drove a large set of Opus first-alpha completion phases, especially
around the alpha host, diagnostics, package tooling, networking hardening,
render-scaling, UI primitives, and the final `Opus.*` identity.

Main contribution areas:

- Added external consumer assembly loading through the alpha host, including
  shared startup policy for window and smoke modes and documentation for the
  `--consumer` flow.
- Hardened UDP and session networking: peer caps, bounded inbound queues,
  per-peer token-bucket rate limiting, server guard counters, telemetry
  surfacing, failure-report integration, and soak-report integration.
- Added package-manifest generation, package-generation CLI support, large-file
  validation budgets, bounded glTF reads, save-frame length checks, and cleanup
  of duplicate content reader code.
- Closed the package trust/container track with signed `.opkg` archives,
  archive verification, safe extraction, signing helpers, pack/verify/unpack
  CLI commands, diagnostics, and tests.
- Added render-scaling features: frustum culling, GPU instancing, coarse LOD
  selection, draw/index counters, and live D3D12 smoke coverage.
- Added UI primitives for freehand ink, persistent annotations, Cyrillic/HUD text
  support, world-space text projection, and tester settings persistence.
- Added alpha-host diagnostics and operator controls: diagnostics root override,
  frame-budget flag, retention activation, screenshot hotkey, overlay-level flag,
  screenshot attachment to reports, async logging opt-in, failure injection, and
  rich DXGI adapter diagnostics.
- Recorded architectural decisions and roadmap updates around SDK direction,
  font choice, package trust, render scaling, UI primitives, and the final
  canonical `Opus.*` assembly/namespace rename.
- Executed the canonical engine identity rename from the previous namespace
  family to `Opus.*`, including source, project files, solution files, embedded
  resource names, identity tests, and documentation.

## Documentation Note

The project documentation was prepared with the assistance of Claude AI
(model: opus 4.8).
