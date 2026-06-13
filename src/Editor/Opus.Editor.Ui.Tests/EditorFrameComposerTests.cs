using System.Linq;
using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class EditorFrameComposerTests
{
    private static readonly Aabb UnitBox = new(new Vector3(-1f), new Vector3(1f));

    [Fact]
    public void Compose_lays_out_chrome_and_projects_the_grid_for_an_empty_scene()
    {
        var document = new EditorDocument("Harbor");

        var view = EditorFrameComposer.Compose(document, new OrbitCamera(), new NullBounds(), 1280, 720);

        view.Chrome.Viewport.Width.Should().Be(1280 - EditorChromeLayout.DslPanelWidth);
        view.ViewportLines.Should().Contain(l => l.Role == ViewportLineRole.GridAxis);
        view.ToolbarText.Should().Contain("Harbor");
        view.PseudoCodeLines.Should().Contain(l => l.StartsWith("scene"));
    }

    [Fact]
    public void Mirror_scroll_slices_the_pseudo_code_from_that_line()
    {
        var document = new EditorDocument("Harbor");
        for (int i = 0; i < 40; i++)
        {
            document.PlaceNode($"n{i}", "m.glb", EditorTransform.Identity);
        }

        string[] full = document.ToPseudoCode().Split('\n');

        var view = EditorFrameComposer.Compose(
            document, new OrbitCamera(), new FixedBounds(UnitBox), 1280, 720, mirrorScroll: 3);

        view.PseudoCodeLines[0].Should().Be(full[3]);
        view.PseudoCodeLines.Count.Should().Be(full.Length - 3);
    }

    [Fact]
    public void Mirror_overscroll_clamps_to_the_last_full_page()
    {
        var document = new EditorDocument("Harbor");
        for (int i = 0; i < 40; i++)
        {
            document.PlaceNode($"n{i}", "m.glb", EditorTransform.Identity);
        }

        string[] full = document.ToPseudoCode().Split('\n');

        var view = EditorFrameComposer.Compose(
            document, new OrbitCamera(), new FixedBounds(UnitBox), 1280, 720, mirrorScroll: 100_000);

        int capacity = EditorFrameDrawer.MirrorLineCapacity(view.Chrome.DslPanel);
        view.PseudoCodeLines.Count.Should().Be(capacity, "the overscroll lands on the last full page");
        view.PseudoCodeLines[^1].Should().Be(full[^1], "the mirror's last line stays reachable");
    }

    [Fact]
    public void The_stats_overlay_composes_only_when_visible()
    {
        var document = new EditorDocument("Harbor");

        var hidden = EditorFrameComposer.Compose(document, new OrbitCamera(), new NullBounds(), 1280, 720);
        var shown = EditorFrameComposer.Compose(
            document, new OrbitCamera(), new NullBounds(), 1280, 720, statsVisible: true);

        hidden.Stats.Should().BeNull();
        shown.Stats.Should().NotBeNull();
        shown.Stats!.Rows.Should().NotBeEmpty();
    }

    [Fact]
    public void Compose_reports_node_count_and_selection_in_the_status_line()
    {
        var document = new EditorDocument("Harbor");
        var id = document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);

        var view = EditorFrameComposer.Compose(document, new OrbitCamera(), new FixedBounds(UnitBox), 1280, 720);

        view.StatusText.Should().Contain("nodes 1");
        view.StatusText.Should().Contain($"selected #{id.Value} alpha");
        view.ToolbarText.Should().EndWith("*", "an unsaved document is marked dirty");
    }

    [Fact]
    public void A_multi_selection_reports_its_count_in_the_status_line()
    {
        var document = new EditorDocument("Harbor");
        var first = document.PlaceNode("alpha", "m.glb", EditorTransform.Identity);
        document.PlaceNode("bravo", "m.glb", EditorTransform.Identity);
        document.ToggleSelect(SceneElementRef.Node(first));

        var view = EditorFrameComposer.Compose(document, new OrbitCamera(), new FixedBounds(UnitBox), 1280, 720);

        view.StatusText.Should().Contain("selected: 2", "a multi-selection has no single name to print");
    }

    [Fact]
    public void Compose_reports_a_selected_light_in_the_status_line()
    {
        var document = new EditorDocument("Harbor");
        var lamp = document.AddNewPointLight(Float3.Zero);

        var view = EditorFrameComposer.Compose(document, new OrbitCamera(), new NullBounds(), 1280, 720);

        view.StatusText.Should().Contain("lights 1");
        view.StatusText.Should().Contain($"selected light *{lamp.Value} light {lamp.Value}");
    }

    [Fact]
    public void The_selected_node_contributes_selection_role_lines()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);

        var view = EditorFrameComposer.Compose(document, new OrbitCamera(), new FixedBounds(UnitBox), 1280, 720);

        view.ViewportLines.Should().Contain(l => l.Role == ViewportLineRole.Selection);
    }

    [Fact]
    public void A_selected_node_draws_a_translate_gizmo()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);

        var view = EditorFrameComposer.Compose(document, new OrbitCamera(), new FixedBounds(UnitBox), 1280, 720);

        view.ViewportLines.Select(l => l.Role).Should().Contain(
            new[] { ViewportLineRole.GizmoX, ViewportLineRole.GizmoY, ViewportLineRole.GizmoZ });
    }

    [Fact]
    public void The_active_gizmo_axis_takes_the_highlight_role()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);

        var view = EditorFrameComposer.Compose(
            document, new OrbitCamera(), new FixedBounds(UnitBox), 1280, 720, GizmoAxis.Y);

        var roles = view.ViewportLines.Select(l => l.Role).ToList();
        roles.Should().Contain(ViewportLineRole.GizmoActive);
        roles.Count(r => r == ViewportLineRole.GizmoY).Should().Be(
            1, "the dragged handle switched to the highlight role; only the corner gnomon's Y arm remains");
    }

    [Fact]
    public void A_selected_node_in_rotate_mode_draws_rotation_rings()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);

        var view = EditorFrameComposer.Compose(
            document, new OrbitCamera(), new FixedBounds(UnitBox), 1280, 720, GizmoAxis.None,
            gizmoMode: GizmoMode.Rotate);

        var gizmoRoles = new[] { ViewportLineRole.GizmoX, ViewportLineRole.GizmoY, ViewportLineRole.GizmoZ };
        view.ViewportLines.Select(l => l.Role).Should().Contain(gizmoRoles);
        view.ViewportLines.Count(l => gizmoRoles.Contains(l.Role)).Should()
            .BeGreaterThan(3, "the rings tessellate into far more segments than the three move handles");
    }

    [Fact]
    public void A_selected_light_draws_a_translate_gizmo_at_its_position()
    {
        var document = new EditorDocument("Harbor");
        document.AddNewPointLight(Float3.Zero);

        var view = EditorFrameComposer.Compose(document, new OrbitCamera(), new NullBounds(), 1280, 720);

        view.ViewportLines.Select(l => l.Role).Should().Contain(
            new[] { ViewportLineRole.GizmoX, ViewportLineRole.GizmoY, ViewportLineRole.GizmoZ });
    }

    [Fact]
    public void A_selected_point_light_draws_no_gizmo_in_scale_mode()
    {
        var document = new EditorDocument("Harbor");
        document.AddNewPointLight(Float3.Zero);

        var view = EditorFrameComposer.Compose(
            document, new OrbitCamera(), new NullBounds(), 1280, 720, GizmoAxis.None,
            gizmoMode: GizmoMode.Scale);

        var roles = view.ViewportLines.Select(l => l.Role).ToList();
        roles.Count(r => r is ViewportLineRole.GizmoX or ViewportLineRole.GizmoY or ViewportLineRole.GizmoZ)
            .Should().Be(3, "only the corner gnomon's three arms — a point light has no scale gizmo");
        roles.Should().NotContain(ViewportLineRole.GizmoActive);
    }

    [Fact]
    public void An_empty_scene_draws_no_gizmo()
    {
        var view = EditorFrameComposer.Compose(new EditorDocument("Harbor"), new OrbitCamera(), new NullBounds(), 1280, 720);

        var roles = view.ViewportLines.Select(l => l.Role).ToList();
        roles.Count(r => r == ViewportLineRole.GizmoX).Should().Be(1, "only the corner gnomon's X arm");
        roles.Should().NotContain(ViewportLineRole.GizmoActive);
    }

    [Fact]
    public void The_toolbar_title_truncates_on_a_narrow_window_instead_of_running_under_the_buttons()
    {
        var document = new EditorDocument("A very long harbor scene document name that cannot possibly fit");

        var wide = EditorFrameComposer.Compose(document, new OrbitCamera(), new FixedBounds(UnitBox), 1920, 720);
        var narrow = EditorFrameComposer.Compose(document, new OrbitCamera(), new FixedBounds(UnitBox), 1180, 720);

        wide.ToolbarText.Should().Contain(document.Name, "a wide window shows the full title");
        narrow.ToolbarText.Should().EndWith("...", "a narrow window clips the title cleanly");
        narrow.ToolbarText.Length.Should().BeLessThan(wide.ToolbarText.Length);
    }

    [Fact]
    public void A_placed_node_enables_the_toolbar_undo_and_delete_buttons()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);

        var view = EditorFrameComposer.Compose(document, new OrbitCamera(), new FixedBounds(UnitBox), 1280, 720);

        view.ToolbarButtons.Should().HaveCount(13, "eight creation buttons join the five document actions");
        view.ToolbarButtons.Single(b => b.Action == EditorToolbarAction.Undo).Enabled.Should().BeTrue();
        view.ToolbarButtons.Single(b => b.Action == EditorToolbarAction.Delete).Enabled.Should().BeTrue();
        view.ToolbarButtons.Single(b => b.Action == EditorToolbarAction.Save).Enabled.Should()
            .BeTrue("placing a node leaves unsaved edits");
    }

    [Fact]
    public void The_outliner_lists_the_scene_nodes()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        document.PlaceNode("bravo", "models/tank.glb", EditorTransform.Identity);

        var view = EditorFrameComposer.Compose(document, new OrbitCamera(), new FixedBounds(UnitBox), 1280, 720);

        view.OutlinerRows.Should().HaveCount(2);
        view.OutlinerRows.Should().Contain(r => r.Label.Contains("alpha"));
        view.OutlinerHeader.Should().Be("scene tree");
    }

    [Fact]
    public void An_empty_document_disables_undo_and_delete()
    {
        var view = EditorFrameComposer.Compose(new EditorDocument("Harbor"), new OrbitCamera(), new NullBounds(), 1280, 720);

        view.ToolbarButtons.Single(b => b.Action == EditorToolbarAction.Undo).Enabled.Should().BeFalse();
        view.ToolbarButtons.Single(b => b.Action == EditorToolbarAction.Delete).Enabled.Should().BeFalse();
        view.ToolbarButtons.Single(b => b.Action == EditorToolbarAction.Save).Enabled.Should()
            .BeFalse("a freshly loaded document has nothing to save");
    }

    [Fact]
    public void The_default_chrome_is_english()
    {
        var view = EditorFrameComposer.Compose(new EditorDocument("Harbor"), new OrbitCamera(), new NullBounds(), 1280, 720);

        view.PseudoCodeHeader.Should().Be("pseudo-code");
        view.StatusText.Should().Contain("nodes");
        view.StatusText.Should().Contain("no selection");
        view.StatusText.Should().Contain("gizmo move", "the default gizmo mode is shown in the status line");
    }

    [Fact]
    public void The_status_line_shows_the_rename_buffer_while_renaming()
    {
        var document = new EditorDocument("Harbor");
        var id = document.PlaceNode("alpha", null, EditorTransform.Identity);

        var view = EditorFrameComposer.Compose(
            document, new OrbitCamera(), new NullBounds(), 1280, 720, GizmoAxis.None,
            rename: new RenameState(SceneElementRef.Node(id), "alp"));

        view.StatusText.Should().Contain("renaming: alp_");
        view.OutlinerRows[0].Label.Should().Be($"#{id.Value} alp_");
    }

    [Fact]
    public void The_status_line_reports_the_active_gizmo_mode()
    {
        var view = EditorFrameComposer.Compose(
            new EditorDocument("Harbor"), new OrbitCamera(), new NullBounds(), 1280, 720, GizmoAxis.None,
            gizmoMode: GizmoMode.Rotate);

        view.StatusText.Should().Contain("gizmo rotate");
    }

    [Fact]
    public void The_help_overlay_is_composed_only_when_visible()
    {
        var document = new EditorDocument("Harbor");

        var hidden = EditorFrameComposer.Compose(document, new OrbitCamera(), new NullBounds(), 1280, 720);
        var shown = EditorFrameComposer.Compose(
            document, new OrbitCamera(), new NullBounds(), 1280, 720, GizmoAxis.None, helpVisible: true);

        hidden.Help.Should().BeNull();
        shown.Help.Should().NotBeNull();
        shown.Help!.Title.Should().Be("Shortcuts");
        shown.Help!.Entries.Should().NotBeEmpty();
    }

    [Fact]
    public void The_help_overlay_follows_the_chrome_language()
    {
        var view = EditorFrameComposer.Compose(
            new EditorDocument("Harbor"), new OrbitCamera(), new NullBounds(), 1280, 720,
            GizmoAxis.None, EditorChromeStrings.Russian, helpVisible: true);

        view.Help!.Title.Should().Be("Горячие клавиши");
    }

    [Fact]
    public void Russian_chrome_localises_the_header_and_status_labels()
    {
        var view = EditorFrameComposer.Compose(
            new EditorDocument("Harbor"), new OrbitCamera(), new NullBounds(), 1280, 720,
            GizmoAxis.None, EditorChromeStrings.Russian);

        view.PseudoCodeHeader.Should().Be("псевдокод");
        view.StatusText.Should().Contain("узлы");
        view.StatusText.Should().Contain("нет выделения");
    }

    [Fact]
    public void An_active_marquee_draws_its_rectangle_in_window_pixels()
    {
        var document = new EditorDocument("Harbor");
        var marquee = new MarqueeState(new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.5f));

        var view = EditorFrameComposer.Compose(
            document, new OrbitCamera(), new NullBounds(), 1280, 720, marquee: marquee);

        var box = view.ViewportLines.Where(l => l.Role == ViewportLineRole.Marquee).ToList();
        box.Should().HaveCount(4, "the rubber band is four screen-space edges");
        var viewport = view.Chrome.Viewport;
        var expectedTopLeft = new Vector2(
            viewport.X + (0.25f * viewport.Width), viewport.Y + (0.25f * viewport.Height));
        box[0].A.Should().Be(expectedTopLeft);
        EditorViewportColors.ForRole(ViewportLineRole.Marquee).Should().Be(EditorViewportColors.Marquee);
    }

    [Fact]
    public void Without_a_marquee_no_marquee_lines_are_composed()
    {
        var view = EditorFrameComposer.Compose(
            new EditorDocument("Harbor"), new OrbitCamera(), new NullBounds(), 1280, 720);

        view.ViewportLines.Should().NotContain(l => l.Role == ViewportLineRole.Marquee);
    }

    [Fact]
    public void The_status_line_shows_the_save_as_buffer_while_naming()
    {
        var view = EditorFrameComposer.Compose(
            new EditorDocument("Harbor"), new OrbitCamera(), new NullBounds(), 1280, 720,
            saveAs: new SaveAsState("Harbor-2"));

        view.StatusText.Should().Contain("save as: Harbor-2_");
    }
}
