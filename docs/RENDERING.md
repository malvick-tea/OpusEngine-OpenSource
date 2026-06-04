# Rendering, RHI, And Frame Graph

This document explains how the Opus rendering side is shaped and how to change
it without mixing backend, scene, and UI responsibilities.

## The Rendering Stack

```text
Opus.Engine.Rhi
  -> backend-agnostic device, buffer, texture, command contracts

Opus.Engine.Rhi.D3D12
  -> D3D12 device, swap chain, command list, descriptors, root signatures,
     pipelines, texture readback, screenshots, D3D12 frame graph

Opus.Engine.FrameGraph
  -> abstract pass/resource vocabulary

Opus.Engine.Renderer
  -> camera, lighting, scene viewport contracts

Opus.Engine.Renderer.Direct3D12
  -> glTF GPU assets, material atlases, scene passes, instancing, culling,
     LOD, forward rendering, tonemapping

Opus.Engine.Ui
  -> draw-surface contracts and screen model

Opus.Engine.Ui.Direct3D12
  -> quad batches, glyph atlas, text layout, ink tessellation
```

The key rule is simple: contracts go up the stack, D3D12 details stay down the
stack.

## RHI Contracts

Use `Opus.Engine.Rhi` when you need a concept that a renderer can talk about
without knowing the backend. Examples:

- buffer descriptions;
- texture descriptions;
- usage flags;
- command-list interface;
- device capabilities.

Do not add D3D12-only details to the RHI contract unless the abstraction is
meant to grow a second backend later.

## D3D12 Backend

Use `Opus.Engine.Rhi.D3D12` for:

- adapter enumeration;
- debug layer setup;
- device creation;
- command queues;
- command lists;
- descriptor heaps;
- root signatures;
- pipeline state;
- D3D12 resources;
- swap chain;
- screenshots and readback.

Backend classes are allowed to expose D3D12 handles. Higher layers should only
see those handles when they are explicitly D3D12-specific.

## Frame Graph

The frame graph coordinates passes and resources for one frame.

Important types:

- `FrameGraphResource`
- `IRenderPass`
- `D3D12FrameGraph`
- `D3D12FrameGraphBuilder`
- `D3D12RenderPass`
- `D3D12RenderPassContext`

The D3D12 frame graph stores:

- imported textures;
- pass list;
- usage declarations;
- planned barriers;
- final-state hints;
- wrappers that must be disposed after a frame.

Typical flow:

```text
Reset()
  -> import resources
  -> add passes
  -> declare usage
  -> ensure final states
  -> compile barrier plan
  -> execute passes
```

## Adding A Frame Graph Pass

1. Decide whether the pass belongs in renderer code or the RHI backend.
2. Define a small pass type derived from `D3D12RenderPass`.
3. Store only the resources and parameters needed by that pass.
4. Declare resource usage through the builder when the pass participates in
   barrier planning.
5. Record commands in the pass.
6. Add the pass to the graph from the renderer orchestration code.
7. Ensure final resource states for imported resources that leave the frame.
8. Add a test for resource usage and ordering if the pass affects barriers.

Avoid large passes that create resources, compile shaders, load assets, and
record draw commands all at once. Resource lifetime should be owned by a renderer
object or target object; passes should record work.

## Scene Renderer

`D3D12ForwardSceneRenderer` is the main D3D12 scene renderer. It owns:

- scene root signature and pipeline;
- tonemap root signature and pipeline;
- scene constant buffers;
- instance buffer rings;
- HDR and depth targets;
- layered render support;
- last-frame draw counters.

It composes:

```text
ForwardScenePass
  -> writes HDR + depth

TonemapPass
  -> writes final color target
```

For multi-layer rendering, `ForwardSceneRenderLayer` supplies layer-specific
draws, materials, cameras, lighting, and post effects. The renderer can render
multiple forward layers into shared targets and tonemap once.

## Adding Scene Data

Scene data normally enters through one of these paths:

- generated primitives;
- `D3D12GltfSceneLoader`;
- caller-supplied `SceneNodeDraw` lists;
- scene layers;
- material atlas builders.

Workflow:

1. Keep CPU-side planning separate from GPU upload.
2. Add or extend a data type for the plan.
3. Test the plan without a GPU.
4. Add upload code.
5. Wire the uploaded asset into the renderer.
6. Add a D3D12 smoke test only when GPU behavior matters.

## Materials

Material paths include:

- `IMaterialAtlas`;
- `SingleTextureAtlas`;
- `MultiMaterialAtlas`;
- `ExternalMaterialAtlasBuilder`;
- `PbrMaterialMaps`;

The renderer expects a material lookup it can bind during draw recording. Keep
catalog or content parsing outside the renderer. Convert parsed content into
renderer-ready material plans before allocation.

## Instancing

Instancing is built from `SceneNodeDraw` values:

```text
SceneNodeDraw list
  -> SceneInstanceBatch.Build
  -> SceneInstanceBufferRing.Upload
  -> ForwardScenePass draws batches
```

When adding instance data:

1. Update CPU instance structures.
2. Update GPU-side constants or structured buffer layout.
3. Update shaders.
4. Update tests that assert instance batching.
5. Keep struct layout explicit.

## Culling And LOD

CPU culling and LOD are optional:

- `SceneNodeCuller` uses mesh bounds and camera frustum.
- `SceneLodSelector` chooses mesh variants by distance.

Do not hide culling in the draw loop. Keep draw-list transforms pure and testable
so renderer tests can assert the final draw list before GPU work.

## UI Rendering

The UI stack starts with `IDrawSurface`. Screens call methods like fill rect,
stroke rect, line, text, and model drawing. The D3D12 backend turns those calls
into quad batches and text geometry.

Important types:

- `IDrawSurface`
- `IScreen`
- `D3D12DrawSurface`
- `UiQuadBatch`
- `UiQuadGeometry`
- `D3D12FontAtlas`
- `GlyphAtlasBaker`
- `UiTextLayout`
- `UiInkTessellator`

When changing UI rendering:

1. Keep screen state in the application/UI layer.
2. Keep draw commands backend-agnostic.
3. Add pure geometry tests first.
4. Add D3D12 tests for atlas, batch, or shader behavior.

## Fonts

The D3D12 UI backend resolves a usable host font. The important behavior is that
the font source can cover the requested codepoint bands and bake glyphs into the
atlas.

When adding text coverage:

1. Add codepoints to the relevant band.
2. Confirm a face can resolve locally.
3. Check atlas capacity.
4. Add layout tests for bounds and wrapping.

## Shaders And Pipelines

Pipeline creation lives in D3D12 factories:

- graphics pipeline factories;
- root signature factories;
- shader compiler.

When changing a shader contract:

1. Update CPU constants.
2. Update root signature or descriptor layout.
3. Update shader code.
4. Update pipeline factory.
5. Update tests that cover constant sizes or draw behavior.

Keep numeric GPU contracts stable unless the shader and CPU side are changed
together.

## Debugging Renderer Problems

Use this order:

1. Confirm the CPU scene plan.
2. Confirm material atlas contents.
3. Confirm instance batch counts.
4. Confirm resource states and final states.
5. Confirm shader/root signature compatibility.
6. Confirm viewport and target sizes.
7. Capture a screenshot or graphics debugger frame.

Do not start with shader changes when the scene plan may already be empty.

## Test Map

Useful test areas:

- `Opus.Engine.Renderer.Tests` for renderer data and planning.
- `Opus.Engine.Ui.Direct3D12.Tests` for UI geometry, text, atlas, ink, and
  projection.
- `Opus.Engine.Direct3D12.Tests` for D3D12 host and rendering checks.
- `Opus.Engine.Host.Windows.Direct3D12.Tests` for host/session behavior.

Add pure tests where possible. Add D3D12 tests when the behavior depends on the
backend.

## Review Checklist

- No D3D12 handles leaked into backend-agnostic contracts.
- Passes record commands but do not own broad setup.
- Resource final states are explicit.
- Shader and CPU constant layouts match.
- Draw counts and batch counts are testable.
- UI behavior has backend-free tests.
- D3D12 behavior has a focused backend test.
