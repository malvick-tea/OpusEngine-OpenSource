# Rendering, RHI, And UI Drawing

Opus keeps rendering responsibilities split into contracts, backend code,
scene-renderer orchestration, and UI drawing. The production graphics path in
this snapshot is Direct3D 12.

## Stack Overview

```text
Opus.Engine.Rhi
  Backend-neutral GPU vocabulary: devices, buffers, textures, command lists,
  capabilities, resource descriptions.

Opus.Engine.Rhi.D3D12
  D3D12 implementation: adapter/device, descriptor heaps, swap chain, command
  lists, root signatures, pipeline factories, frame graph, screenshots,
  readback.

Opus.Engine.FrameGraph
  Abstract pass/resource vocabulary used by backend frame-graph code.

Opus.Engine.Renderer
  Camera, lighting, scene viewport, renderer-facing contracts.

Opus.Engine.Renderer.Direct3D12
  glTF GPU assets, material atlases, scene draw lists, instancing, culling, LOD,
  forward scene pass, tonemap pass.

Opus.Engine.Ui
  Backend-neutral draw-surface and screen contracts.

Opus.Engine.Ui.Direct3D12
  Immediate 2D draw surface over D3D12: quads, lines, glyph atlas, text layout,
  ink, world-space text projection.

Opus.Editor.Direct3D12
  Editor-specific surface that draws composed editor frames through the same
  D3D12 UI backend.
```

The important rule: contracts flow upward, D3D12 details stay downward.

## RHI Contracts

Use `Opus.Engine.Rhi` when a concept should be visible to code that does not
care about the backend.

Examples:

- `IRhiDevice`
- `IRhiCommandList`
- `IRhiBuffer`
- `IRhiTexture`
- `RhiCapabilities`
- `RhiBufferDescription`
- `RhiTextureDescription`
- `RhiTextureFormat`

Do not put D3D12-only details into RHI unless they are intentionally represented
as backend-neutral concepts.

## D3D12 Backend

Use `Opus.Engine.Rhi.D3D12` for:

- adapter enumeration;
- debug layer setup;
- device creation;
- descriptor heaps;
- upload buffers;
- command queues and command lists;
- root signatures;
- graphics and compute pipeline factories;
- swap chain;
- texture readback;
- PNG/BMP screenshots;
- D3D12 frame graph.

Backend classes can expose D3D12 handles when the class itself is clearly
D3D12-specific. Higher layers should not require those handles unless the
boundary explicitly says it is a D3D12 adapter.

## Frame Graph

The D3D12 frame graph coordinates pass order and resource state transitions for
one frame.

Important types:

- `FrameGraphResource`
- `IRenderPass`
- `D3D12FrameGraph`
- `D3D12FrameGraphBuilder`
- `D3D12RenderPass`
- `D3D12RenderPassContext`

Typical flow:

```text
Reset
  -> import or create resources
  -> add passes
  -> declare color/depth/sample usage
  -> ensure final states
  -> compile barrier plan
  -> execute passes
```

When adding a pass:

1. Decide whether the pass belongs in renderer code or in the RHI backend.
2. Keep pass objects narrow: parameters in, command recording out.
3. Let renderer or target objects own long-lived resource allocation.
4. Declare usage through the frame graph builder.
5. Add tests for resource usage, ordering, and final states when those change.

## Forward Scene Renderer

`D3D12ForwardSceneRenderer` is the main D3D12 scene renderer.

It owns:

- scene root signature and pipeline state;
- tonemap root signature and pipeline state;
- scene constant buffers;
- instance buffer ring;
- HDR and depth targets;
- draw counters for tests and diagnostics.

It composes:

```text
ForwardScenePass
  -> clears and writes HDR color + depth

TonemapPass
  -> ACES-tonemaps HDR into the final color target
```

The renderer can draw to the swap-chain back buffer or into a supplied
`SceneRenderTarget`. The alpha host uses the offscreen path to draw a 3D scene
into a viewport region and then composites UI over the swap chain.

## Scene Data

Scene data enters the renderer through:

- procedural primitives;
- `D3D12GltfSceneLoader`;
- caller-supplied `SceneNodeDraw` lists;
- material atlas builders;
- optional mesh bounds for culling;
- optional mesh LOD chains for distance-based demotion.

Keep CPU planning separate from GPU allocation:

1. Build a data plan.
2. Test the plan without a GPU where possible.
3. Upload resources.
4. Wire the pass.
5. Add a D3D12 smoke test when pixels, resources, or backend behavior are the
   subject.

## Materials

Material rendering is centered on `IMaterialAtlas`.

Important types:

- `SingleTextureAtlas`
- `MultiMaterialAtlas`
- `ExternalMaterialAtlasPlan`
- `ExternalMaterialAtlasBuilder`
- `PbrMaterialMaps`

The scene renderer binds the atlas and resolves material map descriptors per
primitive. Content parsing and package validation stay outside the renderer;
renderer code consumes already-planned material data.

## Instancing

Instancing starts from `SceneNodeDraw` values:

```text
SceneNodeDraw list
  -> SceneInstanceBatch.Build
  -> SceneInstanceBufferRing.Upload
  -> ForwardScenePass.DrawIndexedInstanced
```

The pass issues one draw per mesh primitive per batch, with per-instance world
and tint data read by the shader. This keeps draw-call count stable when many
scene nodes share the same mesh.

When changing instance data:

1. Update CPU-side structures.
2. Update GPU constants or structured-buffer layout.
3. Update shaders.
4. Update batching and draw-count tests.
5. Keep struct layout explicit.

## Culling And LOD

Culling and LOD are opt-in planning steps:

- `SceneNodeCuller` drops nodes fully outside the camera frustum.
- `SceneLodSelector` chooses cheaper mesh variants by distance.

The renderer exposes last-frame counters:

- `LastDrawnPrimitiveCount`
- `LastDrawCallCount`
- `LastCulledNodeCount`
- `LastDrawnIndexCount`
- `LastLodDemotedNodeCount`

Keep culling and LOD visible as planning behavior. Do not bury them in an
unobservable draw loop.

## Alpha Host Composition

The Windows/D3D12 alpha host uses `D3D12OpusApplication` as the production
`IOpusApplication` implementation.

Frame shape:

```text
OpusHost.Step
  -> D3D12OpusApplication.Render
  -> build consumer or sample draw list
  -> render 3D scene into a viewport target
  -> begin D3D12 UI frame
  -> draw diagnostics, frame, text, screenshot hooks
  -> present
```

The host also owns:

- frame metrics;
- frame budget warnings;
- diagnostic overlay coordination;
- consumer integration bridge;
- screenshot queue and readback;
- resize bridge.

## UI Draw Surface

The UI stack starts with `IDrawSurface`. Screens and editor drawers issue
backend-neutral commands such as rectangles, lines, text, and textured/model
quads. The D3D12 backend turns those commands into GPU batches.

Important types:

- `IDrawSurface`
- `IScreen`
- `D3D12DrawSurface`
- `D3D12UiFrameLoop`
- `UiQuadBatch`
- `UiQuadGeometry`
- `D3D12FontAtlas`
- `GlyphAtlasBaker`
- `UiTextLayout`
- `UiInkTessellator`

When changing UI rendering:

1. Keep screen state in the UI or app layer.
2. Keep draw commands backend-neutral.
3. Add pure geometry/layout tests first.
4. Add D3D12 tests when atlas, batch, shader, or readback behavior changes.

## Editor Rendering

The editor does not create a second 3D scene renderer. It uses a pure composer
and the existing D3D12 UI draw surface:

```text
EditorDocument
  -> EditorFrameComposer
  -> EditorFrameView
  -> EditorFrameDrawer
  -> EditorRenderSurface
  -> D3D12DrawSurface
```

The editor viewport is a projected wire/line authoring view. This keeps the
editor render seam small and makes most editor behavior testable without a GPU.

## Fonts And Text

The D3D12 UI backend resolves a usable font face and bakes requested glyphs into
a glyph atlas. The editor bakes printable ASCII plus Cyrillic coverage so English
and Russian chrome can render in the live window.

When adding text coverage:

1. Add the codepoints to the corpus or relevant band.
2. Confirm a face resolves locally.
3. Check atlas capacity.
4. Add layout tests for bounds, wrapping, and glyph presence.

## Shaders And Pipelines

Pipeline creation belongs in D3D12 factories:

- graphics pipeline factories;
- compute pipeline factories;
- root signature factories;
- shader compiler.

When changing a shader contract:

1. Update CPU constants.
2. Update root signature or descriptor layout.
3. Update shader code.
4. Update pipeline factory.
5. Update tests that cover constant sizes, root bindings, or draw behavior.

Shader and CPU layouts must change together.

## Screenshots And Readback

Screenshot support uses D3D12 readback resources and PNG/BMP writers.

Relevant types:

- `D3D12TextureReadback`
- `D3D12Screenshot`
- `D3D12ScreenshotPngWriter`
- `D3D12ScreenshotBmpWriter`
- `D3D12ScreenshotMetadata`

The alpha host and editor both expose screenshot paths. The readback path waits
for GPU completion before reading pixels.

## Debugging Order

For scene-rendering problems:

1. Confirm the CPU draw list.
2. Confirm material atlas contents.
3. Confirm instance batch counts.
4. Confirm culling and LOD counters.
5. Confirm resource states and final states.
6. Confirm root signature and shader compatibility.
7. Confirm viewport and target size.
8. Capture a screenshot or graphics-debugger frame.

For editor rendering problems:

1. Confirm `EditorFrameComposer` output.
2. Confirm projected line counts.
3. Confirm chrome layout rectangles.
4. Confirm glyph coverage.
5. Confirm `EditorRenderSurface` can open a D3D12 session.

## Test Map

Useful test projects:

- `Opus.Engine.Renderer.Tests` for renderer-facing data and pure planning.
- `Opus.Engine.Ui.Direct3D12.Tests` for UI geometry, text, atlas, ink, and
  projection.
- `Opus.Engine.Direct3D12.Tests` for D3D12 renderer smoke and backend behavior.
- `Opus.Engine.Host.Windows.Direct3D12.Tests` for host/session/frame behavior.
- `Opus.Editor.Ui.Tests` for editor frame composition, viewport math, gizmos,
  outliner, inspector, toolbar, and input.
- `Opus.Editor.Direct3D12.Tests` for live editor surface smoke coverage.

Prefer pure tests. Use D3D12 tests only when behavior depends on the backend.

## Review Checklist

- No D3D12 handles leak into backend-neutral contracts.
- Frame-graph usage and final states are explicit.
- Passes record commands but do not own broad setup.
- Renderer planning is testable before GPU work.
- Shader and CPU constant layouts match.
- Draw counts and batch counts are observable.
- UI behavior has backend-free tests.
- D3D12 behavior has focused backend tests.
- Editor rendering stays on the UI draw surface unless a real 3D editor viewport
  is intentionally added.
