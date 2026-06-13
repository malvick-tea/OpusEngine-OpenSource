# Opus UI Editor

The Opus UI Edition editor is the public authoring surface for this snapshot. It
can be used as a live D3D12 editor window or as a CLI for creating, inspecting,
and transforming scene, project, material, and animation documents.

The editor is deliberately layered. The authoring model is pure and GPU-free;
the UI layer is pure and host-free; the Direct3D 12 layer only draws frames; the
app layer owns the console, filesystem, settings, autosave, and the live window
loop.

## Project Map

```text
Opus.Editor.Core
  Pure documents, scene graph, lights, animation graphs, projects, commands,
  undo/redo, serializers, validators, pseudo-code mirrors.

Opus.Editor.Content
  Model summaries, scene content reports, and PBR material-set inspection.

Opus.Editor.Ui
  Viewport camera, picking, gizmos, outliner, inspector, toolbar, chrome layout,
  frame composition, and input mapping. No D3D12, no filesystem.

Opus.Editor.Direct3D12
  The live editor draw surface over the existing D3D12 UI backend.

Opus.App.Editor
  CLI parser, command dispatch, file stores, project workspace resolution,
  settings, autosave, screenshots, and the live window runner.
```

Tests mirror the same boundaries:

```text
Opus.Editor.Core.Tests
Opus.Editor.Content.Tests
Opus.Editor.Ui.Tests
Opus.Editor.Direct3D12.Tests
Opus.App.Editor.Tests
```

## Document Types

The editor works with three human-readable JSON document families. Each uses the
shared persistence settings envelope, schema versioning, indented camel-case
JSON, and corruption-safe load errors.

| Document | Serializer | What it contains |
| --- | --- | --- |
| Scene | `EditorSceneSerializer` | Scene name, nodes, hierarchy, transforms, asset refs, hidden state, lights |
| Project | `EditorProjectSerializer` | Content roots, scene files, animation graphs, material roots |
| Animation graph | `AnimationGraphSerializer` | States, clips, entry state, transitions, blend timings |

Every document also has a deterministic pseudo-code projection:

- `SceneDslWriter`
- `EditorProjectDslWriter`
- `AnimationGraphDslWriter`

The pseudo-code view is not a second source of truth. It is a readable mirror of
the JSON document, useful in the CLI and in the live editor panel.

## Scene Model

`EditorDocument` is the transactional authority for one open scene. It owns:

- the mutable `EditorScene`;
- the undo/redo command stack;
- the current selection set;
- the dirty flag;
- high-level authoring operations.

Scene mutations are represented by commands such as `PlaceNodeCommand`,
`TransformNodeCommand`, `SetNodeParentCommand`, `AddLightCommand`, and
`CompositeSceneCommand`. UI state such as selection is not undoable; authored
scene changes are.

Nodes are immutable `SceneNode` records. A node has:

- a stable id;
- a display name;
- an optional asset reference;
- a local transform;
- an optional parent id;
- a hidden flag.

Lights use their own id sequence and support directional, point, and spot
variants.

## Hierarchy And Grouping

The scene hierarchy supports parented nodes, grouping, ungrouping, and
world-preserving reparenting.

Important behavior:

- `Ctrl+G` groups selected nodes under a new empty parent at their centroid.
- `Ctrl+Shift+G` ungroups selected groups.
- `P` parents the selection under the primary selected node.
- `Shift+P` detaches selected nodes to roots.
- Reparenting keeps world positions stable.
- Ungrouping nested groups promotes children to the nearest surviving ancestor.
- Empty group nodes are removed during ungroup; asset-bearing nodes keep their
  asset and only release children.

The world-space planning lives in `SceneGroupingPlanner`, which is pure and
unit-tested. `EditorDocument` handles id allocation, command execution, and
selection updates.

## Live Window

Open the editor window:

```powershell
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- window
```

Open a scene:

```powershell
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- window .\garage.scene.json
```

Open with a project, content roots, persistent settings, and language:

```powershell
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- window .\garage.scene.json --project .\garage.opusproj.json --content-root .\content --settings .\.local\editor.settings.json --lang en
```

Window options:

- `--project <file>` loads content roots and scene lists from an editor project.
- `--content-root <dir>` is searched by the model browser and bounds resolver.
- `--settings <file>` persists window size, last scene, last project, and
  language.
- `--lang en|ru` selects English or Russian chrome.
- `--frames <n>` caps the window loop for smoke tests.

## Window Workflow

The first screen is the editor itself:

- central viewport;
- top toolbar;
- scene outliner;
- inspector;
- live pseudo-code panel;
- status line;
- optional help and stats overlays.

Core shortcuts:

| Input | Action |
| --- | --- |
| LMB drag | Orbit camera |
| MMB drag | Pan camera |
| Wheel | Zoom viewport, or scroll panel under cursor |
| Click | Select node or light |
| Ctrl+Click | Toggle element in multi-selection |
| Shift+Drag | Box select |
| Ctrl+A | Select every visible element |
| 1-5 | Add cube, sphere, cylinder, plane, cone |
| A / L | Add empty node / point light |
| M | Place model from content roots |
| W / E / R | Move, scale, rotate gizmo mode |
| Drag axis | Transform selection |
| Ctrl while dragging | Snap transform |
| Arrows | Nudge selection on grid |
| F / H | Frame selection / reset camera |
| V | Hide or show selection |
| P / Shift+P | Parent under primary / detach to root |
| Delete | Delete selection |
| Ctrl+R | Rename selection or scene |
| Ctrl+D | Duplicate selection |
| Ctrl+G / Ctrl+Shift+G | Group / ungroup selected nodes |
| Ctrl+C / Ctrl+V | Copy / paste selection |
| Ctrl+Z / Ctrl+Y | Undo / redo |
| Ctrl+S / Ctrl+Shift+S | Save / save as |
| Ctrl+N / Ctrl+O | New scene / open scene browser |
| F1 / F2 / F3 | Help / screenshot / developer stats |
| Esc | Quit |

The full shortcut table is generated by `EditorHelpOverlay` and tested by the UI
test suite.

## CLI Commands

Show the generated help:

```powershell
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- --help
```

Scene commands:

```powershell
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- new .\scene.json --name Garage
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- show .\scene.json
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- dsl .\scene.json
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- place .\scene.json .\models\tank.glb --name Tank --at 0,0,0
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- scene-move .\scene.json 1 --at 3,0,4
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- scene-parent .\scene.json 2 1
```

Light commands:

```powershell
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- light-add .\scene.json point --name Key --at 0,3,0 --color 1,0.9,0.75 --intensity 4 --range 12
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- light-edit .\scene.json 1 --intensity 8
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- light-remove .\scene.json 1
```

Content and material commands:

```powershell
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- inspect .\models\tank.glb
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- report .\scene.json --content-root .\content
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- materials .\content\materials
```

Animation graph commands:

```powershell
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- anim-new .\locomotion.anim.json --name Locomotion
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- anim-state .\locomotion.anim.json Idle --clip idle.glb --loop true --entry
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- anim-transition .\locomotion.anim.json Idle Walk --on move --blend 0.15
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- anim-show .\locomotion.anim.json
```

Project commands:

```powershell
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- project-new .\garage.opusproj.json --name Garage
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- project-add .\garage.opusproj.json content-root .\content
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- project-add .\garage.opusproj.json scene .\scene.json
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- project-check .\garage.opusproj.json
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- project-doctor .\garage.opusproj.json --content-root .\content
```

The editor CLI is table-driven through `EditorCommand`. The same command table
drives dispatch and help text, so command usage cannot drift from the parser.

## Rendering Model

The editor window does not create a second 3D renderer. It draws the editor
scene as projected world-space lines through the existing D3D12 UI draw surface.

That keeps the editor surface small:

```text
EditorDocument
  -> EditorFrameComposer
  -> EditorFrameView
  -> EditorFrameDrawer
  -> EditorRenderSurface
  -> D3D12DrawSurface
```

The content model remains authoring data. Runtime scene rendering continues to
live in the engine D3D12 renderer.

## Testing

Run all editor tests:

```powershell
dotnet test .\src\Editor\Opus.Editor.Core.Tests\Opus.Editor.Core.Tests.csproj -c Release
dotnet test .\src\Editor\Opus.Editor.Content.Tests\Opus.Editor.Content.Tests.csproj -c Release
dotnet test .\src\Editor\Opus.Editor.Ui.Tests\Opus.Editor.Ui.Tests.csproj -c Release
dotnet test .\src\Editor\Opus.Editor.Direct3D12.Tests\Opus.Editor.Direct3D12.Tests.csproj -c Release
dotnet test .\src\Editor\Opus.App.Editor.Tests\Opus.App.Editor.Tests.csproj -c Release
```

Current editor test coverage in the full suite:

```text
Opus.Editor.Core.Tests       194
Opus.App.Editor.Tests        187
Opus.Editor.Content.Tests     11
Opus.Editor.Ui.Tests         426
Opus.Editor.Direct3D12.Tests   9
Total                        827
```

## Where To Change Things

| Change | Start here |
| --- | --- |
| Scene mutation, undo/redo, serializer, pseudo-code | `Opus.Editor.Core` |
| Model/material/content summaries | `Opus.Editor.Content` |
| Viewport behavior, gizmos, selection, layout | `Opus.Editor.Ui` |
| Live D3D12 editor drawing or screenshots | `Opus.Editor.Direct3D12` |
| CLI, file IO, autosave, settings, project workspace | `Opus.App.Editor` |

Keep the pure layers pure. If a feature needs filesystem access, it belongs in
`Opus.App.Editor`. If it needs GPU handles, it belongs in
`Opus.Editor.Direct3D12` or an engine D3D12 backend. If it can be expressed as
document data or UI geometry, keep it headless and test it without a window.
