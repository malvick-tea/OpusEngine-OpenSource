using System;
using System.IO;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Editor.Ui;
using Opus.Engine.Pal.Windows.Direct3D12;
using Opus.Engine.Ui;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Direct3D12.Tests;

/// <summary>
/// Live D3D12 smoke for the editor window seam: opens a real window, composes a frame for a small scene
/// (a placed, selected node), and renders it through <see cref="EditorRenderSurface"/>. Reads the presented
/// back buffer back and proves the editor painted brighter-than-background pixels (grid, wire box, and
/// text) at the window's dimensions with no device-removed, and writes the capture to a PNG the owner can
/// review. Skips cleanly when no D3D12 adapter / SDL video / DXC is available.
/// </summary>
public sealed class EditorWindowSmokeTests
{
    private const int WindowWidth = 1024;
    private const int WindowHeight = 640;

    private static readonly Aabb UnitBox = new(new Vector3(-1f), new Vector3(1f));

    [SkippableFact]
    public void Editor_window_renders_scene_chrome_and_pseudocode()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "The editor window is Windows-only (D3D12).");
        var options = D3D12WindowSessionOptions.Windowed(
            "opus-editor-window-smoke", WindowWidth, WindowHeight, enableDebugLayer: false);
        using var session = D3D12WindowSession.TryOpen(options);
        Skip.If(session is null, "No D3D12 adapter, SDL video, swap chain, or DXC is available on this host.");

        using var surface = EditorRenderSurface.Create(session);

        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        var camera = new OrbitCamera { Target = Vector3.Zero };
        var view = EditorFrameComposer.Compose(
            document, camera, new UnitBoxBounds(), surface.Width, surface.Height);

        var screenshotPath = Path.Combine(Path.GetTempPath(), "opus-editor-window-smoke.png");
        var screenshot = surface.RenderFrameAndCapture(view, screenshotPath);

        screenshot.Width.Should().Be(surface.Width);
        screenshot.Height.Should().Be(surface.Height);
        HasVisiblePixels(screenshot.Rgba8).Should()
            .BeTrue("the editor chrome, grid, wire box, and text should paint above the dark background");
        File.Exists(screenshotPath).Should().BeTrue();
    }

    [SkippableFact]
    public void Editor_window_renders_correctly_after_a_swap_chain_resize()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "The editor window is Windows-only (D3D12).");
        var options = D3D12WindowSessionOptions.Windowed(
            "opus-editor-resize-smoke", WindowWidth, WindowHeight, enableDebugLayer: false, resizable: true);
        using var session = D3D12WindowSession.TryOpen(options);
        Skip.If(session is null, "No D3D12 adapter, SDL video, swap chain, or DXC is available on this host.");

        using var surface = EditorRenderSurface.Create(session);
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        var camera = new OrbitCamera { Target = Vector3.Zero };
        var bounds = new UnitBoxBounds();

        surface.RenderFrame(EditorFrameComposer.Compose(document, camera, bounds, surface.Width, surface.Height));

        const int ResizedWidth = 800;
        const int ResizedHeight = 600;
        session.SwapChain.Resize(ResizedWidth, ResizedHeight);

        var view = EditorFrameComposer.Compose(document, camera, bounds, surface.Width, surface.Height);
        var screenshot = surface.RenderFrameAndCapture(view);

        surface.Width.Should().Be(ResizedWidth);
        screenshot.Width.Should().Be(ResizedWidth);
        screenshot.Height.Should().Be(ResizedHeight);
        HasVisiblePixels(screenshot.Rgba8).Should().BeTrue("the editor must paint a clean frame at the new size");
    }

    [SkippableFact]
    public void Editor_window_draws_the_translate_gizmo_on_the_selection()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "The editor window is Windows-only (D3D12).");
        var options = D3D12WindowSessionOptions.Windowed(
            "opus-editor-gizmo-smoke", WindowWidth, WindowHeight, enableDebugLayer: false);
        using var session = D3D12WindowSession.TryOpen(options);
        Skip.If(session is null, "No D3D12 adapter, SDL video, swap chain, or DXC is available on this host.");

        using var surface = EditorRenderSurface.Create(session);

        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        var camera = new OrbitCamera { Target = Vector3.Zero };
        var view = EditorFrameComposer.Compose(
            document, camera, new UnitBoxBounds(), surface.Width, surface.Height, GizmoAxis.X);

        var screenshotPath = Path.Combine(Path.GetTempPath(), "opus-editor-gizmo-smoke.png");
        var screenshot = surface.RenderFrameAndCapture(view, screenshotPath);

        ContainsColor(screenshot.Rgba8, EditorViewportColors.GizmoY).Should().BeTrue("the Y axis handle renders green");
        ContainsColor(screenshot.Rgba8, EditorViewportColors.GizmoZ).Should().BeTrue("the Z axis handle renders blue");
        ContainsColor(screenshot.Rgba8, EditorViewportColors.GizmoActive).Should()
            .BeTrue("the dragged X axis renders with the highlight colour");
        File.Exists(screenshotPath).Should().BeTrue();
    }

    [SkippableFact]
    public void Editor_window_draws_the_scale_gizmo_on_the_selection()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "The editor window is Windows-only (D3D12).");
        var options = D3D12WindowSessionOptions.Windowed(
            "opus-editor-scale-gizmo-smoke", WindowWidth, WindowHeight, enableDebugLayer: false);
        using var session = D3D12WindowSession.TryOpen(options);
        Skip.If(session is null, "No D3D12 adapter, SDL video, swap chain, or DXC is available on this host.");

        using var surface = EditorRenderSurface.Create(session);

        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        var camera = new OrbitCamera { Target = Vector3.Zero };
        var view = EditorFrameComposer.Compose(
            document, camera, new UnitBoxBounds(), surface.Width, surface.Height, GizmoAxis.X,
            gizmoMode: GizmoMode.Scale);

        var screenshotPath = Path.Combine(Path.GetTempPath(), "opus-editor-scale-gizmo-smoke.png");
        var screenshot = surface.RenderFrameAndCapture(view, screenshotPath);

        ContainsColor(screenshot.Rgba8, EditorViewportColors.GizmoY).Should().BeTrue("the Y scale handle renders green");
        ContainsColor(screenshot.Rgba8, EditorViewportColors.GizmoZ).Should().BeTrue("the Z scale handle renders blue");
        ContainsColor(screenshot.Rgba8, EditorViewportColors.GizmoActive).Should()
            .BeTrue("the dragged X scale axis (handle + tip cube) renders with the highlight colour");
        File.Exists(screenshotPath).Should().BeTrue();
    }

    [SkippableFact]
    public void Editor_window_draws_the_rotation_gizmo_on_the_selection()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "The editor window is Windows-only (D3D12).");
        var options = D3D12WindowSessionOptions.Windowed(
            "opus-editor-rotate-gizmo-smoke", WindowWidth, WindowHeight, enableDebugLayer: false);
        using var session = D3D12WindowSession.TryOpen(options);
        Skip.If(session is null, "No D3D12 adapter, SDL video, swap chain, or DXC is available on this host.");

        using var surface = EditorRenderSurface.Create(session);

        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        var camera = new OrbitCamera { Target = Vector3.Zero };
        var view = EditorFrameComposer.Compose(
            document, camera, new UnitBoxBounds(), surface.Width, surface.Height, GizmoAxis.X,
            gizmoMode: GizmoMode.Rotate);

        var screenshotPath = Path.Combine(Path.GetTempPath(), "opus-editor-rotate-gizmo-smoke.png");
        var screenshot = surface.RenderFrameAndCapture(view, screenshotPath);

        ContainsColor(screenshot.Rgba8, EditorViewportColors.GizmoY).Should().BeTrue("the Y rotation ring renders green");
        ContainsColor(screenshot.Rgba8, EditorViewportColors.GizmoZ).Should().BeTrue("the Z rotation ring renders blue");
        ContainsColor(screenshot.Rgba8, EditorViewportColors.GizmoActive).Should()
            .BeTrue("the active X rotation ring renders with the highlight colour");
        File.Exists(screenshotPath).Should().BeTrue();
    }

    [SkippableFact]
    public void Editor_window_renders_russian_chrome()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "The editor window is Windows-only (D3D12).");
        var options = D3D12WindowSessionOptions.Windowed(
            "opus-editor-ru-smoke", WindowWidth, WindowHeight, enableDebugLayer: false);
        using var session = D3D12WindowSession.TryOpen(options);
        Skip.If(session is null, "No D3D12 adapter, SDL video, swap chain, or DXC is available on this host.");

        using var surface = EditorRenderSurface.Create(session);

        // A Cyrillic document name plus the Russian chrome exercise the full Cyrillic atlas path.
        var document = new EditorDocument("Гавань");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        var camera = new OrbitCamera { Target = Vector3.Zero };
        var view = EditorFrameComposer.Compose(
            document, camera, new UnitBoxBounds(), surface.Width, surface.Height, GizmoAxis.None, EditorChromeStrings.Russian);

        var screenshotPath = Path.Combine(Path.GetTempPath(), "opus-editor-ru-smoke.png");
        var screenshot = surface.RenderFrameAndCapture(view, screenshotPath);

        HasVisiblePixels(screenshot.Rgba8).Should()
            .BeTrue("the Russian chrome and Cyrillic document name must render through the editor atlas");
        File.Exists(screenshotPath).Should().BeTrue();
    }

    [SkippableFact]
    public void Editor_window_draws_every_primitive_shape()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "The editor window is Windows-only (D3D12).");
        var options = D3D12WindowSessionOptions.Windowed(
            "opus-editor-primitives-smoke", WindowWidth, WindowHeight, enableDebugLayer: false);
        using var session = D3D12WindowSession.TryOpen(options);
        Skip.If(session is null, "No D3D12 adapter, SDL video, swap chain, or DXC is available on this host.");

        using var surface = EditorRenderSurface.Create(session);

        // One of each primitive in a row — the full PrimitiveWire shape set rendered through the live
        // line batch, with the cube selected so the selection promotion paints too.
        var document = new EditorDocument("Primitives");
        float x = -4f;
        foreach (var kind in new[]
        {
            ScenePrimitiveKind.Cube, ScenePrimitiveKind.Sphere, ScenePrimitiveKind.Cylinder,
            ScenePrimitiveKind.Plane, ScenePrimitiveKind.Cone,
        })
        {
            document.PlaceNewPrimitive(
                kind, EditorTransform.Identity with { Position = new Float3(x, 0.5f, 0f) });
            x += 2f;
        }

        document.Select(document.Scene.Nodes[0].Id);
        var camera = new OrbitCamera { Target = Vector3.Zero };
        camera.SetDistance(12f);
        var view = EditorFrameComposer.Compose(
            document, camera, new UnitBoxBounds(), surface.Width, surface.Height);

        var screenshotPath = Path.Combine(Path.GetTempPath(), "opus-editor-primitives-smoke.png");
        var screenshot = surface.RenderFrameAndCapture(view, screenshotPath);

        ContainsColor(screenshot.Rgba8, EditorViewportColors.NodeBounds).Should()
            .BeTrue("the unselected primitive wires render in the node colour");
        ContainsColor(screenshot.Rgba8, EditorViewportColors.Selection).Should()
            .BeTrue("the selected cube renders highlighted");
        File.Exists(screenshotPath).Should().BeTrue();
    }

    [SkippableFact]
    public void Editor_window_draws_a_scene_light_glyph()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "The editor window is Windows-only (D3D12).");
        var options = D3D12WindowSessionOptions.Windowed(
            "opus-editor-light-smoke", WindowWidth, WindowHeight, enableDebugLayer: false);
        using var session = D3D12WindowSession.TryOpen(options);
        Skip.If(session is null, "No D3D12 adapter, SDL video, swap chain, or DXC is available on this host.");

        using var surface = EditorRenderSurface.Create(session);

        // A spot light, no selected node — so the only warm-amber pixels in the frame are the light glyph
        // (star + aim ray), not a selection box or an active gizmo handle.
        var document = new EditorDocument("Harbor");
        document.AddLight(SceneLight.CreateSpot("torch") with { Position = new Float3(1.5f, 2f, 0f) });
        var camera = new OrbitCamera { Target = Vector3.Zero };
        var view = EditorFrameComposer.Compose(
            document, camera, new UnitBoxBounds(), surface.Width, surface.Height);

        var screenshotPath = Path.Combine(Path.GetTempPath(), "opus-editor-light-smoke.png");
        var screenshot = surface.RenderFrameAndCapture(view, screenshotPath);

        ContainsColor(screenshot.Rgba8, EditorViewportColors.Light).Should()
            .BeTrue("the scene light's star-and-aim-ray glyph renders in the light colour");
        File.Exists(screenshotPath).Should().BeTrue();
    }

    [SkippableFact]
    public void Editor_window_draws_the_marquee_rubber_band()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "The editor window is Windows-only (D3D12).");
        var options = D3D12WindowSessionOptions.Windowed(
            "opus-editor-marquee-smoke", WindowWidth, WindowHeight, enableDebugLayer: false);
        using var session = D3D12WindowSession.TryOpen(options);
        Skip.If(session is null, "No D3D12 adapter, SDL video, swap chain, or DXC is available on this host.");

        using var surface = EditorRenderSurface.Create(session);

        // An empty scene with no selection — the only cool-blue pixels in the frame are the in-progress
        // box-select rubber band the composer appends in screen space.
        var document = new EditorDocument("Harbor");
        var camera = new OrbitCamera { Target = Vector3.Zero };
        var marquee = new MarqueeState(new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.7f));
        var view = EditorFrameComposer.Compose(
            document, camera, new UnitBoxBounds(), surface.Width, surface.Height, marquee: marquee);

        var screenshotPath = Path.Combine(Path.GetTempPath(), "opus-editor-marquee-smoke.png");
        var screenshot = surface.RenderFrameAndCapture(view, screenshotPath);

        ContainsColor(screenshot.Rgba8, EditorViewportColors.Marquee).Should()
            .BeTrue("the box-select rubber band renders in the marquee colour");
        File.Exists(screenshotPath).Should().BeTrue();
    }

    // The editor clears to a dark background; chrome text, the selection box, and the grid axis all sit
    // well above this threshold, so a visible frame has at least one bright pixel.
    private static bool HasVisiblePixels(byte[] rgba) =>
        rgba.Chunk(4).Any(pixel => Math.Max(pixel[0], Math.Max(pixel[1], pixel[2])) > 100);

    // The gizmo handles are solid 3px lines, so a matching frame holds pixels near each axis colour.
    private static bool ContainsColor(byte[] rgba, Color color)
    {
        const int Tolerance = 40;
        return rgba.Chunk(4).Any(pixel =>
            Math.Abs(pixel[0] - color.R) <= Tolerance &&
            Math.Abs(pixel[1] - color.G) <= Tolerance &&
            Math.Abs(pixel[2] - color.B) <= Tolerance);
    }

    private sealed class UnitBoxBounds : IModelBoundsSource
    {
        public Aabb? TryGetLocalBounds(string assetRef) => UnitBox;
    }
}
