using System;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class EditorStatsOverlayTests
{
    private static readonly EditorPanelRect Viewport = new(0, 32, 960, 600);

    [Fact]
    public void Rows_report_the_live_document_tallies()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("tank", "models/tank.glb", EditorTransform.Identity);
        document.PlaceNewPrimitive(ScenePrimitiveKind.Cube, EditorTransform.Identity);
        var empty = document.PlaceNewNode(EditorTransform.Identity);
        document.AddNewPointLight(Float3.Zero);
        document.SetNodeHidden(empty, true);

        var view = EditorStatsOverlay.Build(
            Viewport, document, new OrbitCamera(), GizmoMode.Rotate, EditorLanguage.English);

        var rows = view.Rows.ToDictionary(r => r.Label, r => r.Value, StringComparer.Ordinal);
        rows["nodes"].Should().Be("3 (1 models, 1 shapes, 1 empty)");
        rows["lights"].Should().Be("1 (0 dir, 1 point, 0 spot)");
        rows["hidden"].Should().Be("1");
        rows["selected"].Should().Be("1");
        rows["undo / redo"].Should().Be("5 / 0");
        rows["gizmo"].Should().Be("rotate");
    }

    [Fact]
    public void The_camera_rows_format_the_pose_invariantly()
    {
        var document = new EditorDocument("Harbor");
        var camera = new OrbitCamera { Target = new Vector3(1.5f, 0f, -2.25f) };

        var view = EditorStatsOverlay.Build(
            Viewport, document, camera, GizmoMode.Translate, EditorLanguage.English);

        view.Rows.Single(r => r.Label == "camera target").Value.Should().Be("1.5, 0, -2.25");
    }

    [Fact]
    public void The_table_is_localised()
    {
        var document = new EditorDocument("Harbor");

        var english = EditorStatsOverlay.Build(
            Viewport, document, new OrbitCamera(), GizmoMode.Translate, EditorLanguage.English);
        var russian = EditorStatsOverlay.Build(
            Viewport, document, new OrbitCamera(), GizmoMode.Translate, EditorLanguage.Russian);

        russian.Rows.Should().HaveCount(english.Rows.Count);
        russian.Title.Should().NotBe(english.Title);
        russian.Rows.Select(r => r.Label).Should().NotIntersectWith(english.Rows.Select(r => r.Label));
    }

    [Fact]
    public void The_panel_anchors_in_the_viewport_top_right()
    {
        var view = EditorStatsOverlay.Build(
            Viewport, new EditorDocument("Harbor"), new OrbitCamera(), GizmoMode.Translate, EditorLanguage.English);

        view.Panel.Right.Should().Be(Viewport.Right - EditorStatsOverlay.ViewportMargin);
        view.Panel.Y.Should().Be(Viewport.Y + EditorStatsOverlay.ViewportMargin);
        view.Panel.Bottom.Should().BeLessThanOrEqualTo(Viewport.Bottom);
    }
}
