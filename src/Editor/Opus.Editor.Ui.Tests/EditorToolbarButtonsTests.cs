using System.Linq;
using FluentAssertions;
using Opus.Editor.Ui;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class EditorToolbarButtonsTests
{
    private static readonly EditorPanelRect Toolbar = new(0, 0, 1280, 32);
    private static readonly EditorToolbarState AllEnabled =
        new(CanUndo: true, CanRedo: true, HasSelection: true, IsDirty: true);

    private static EditorPanelRect RectOf(
        System.Collections.Generic.IReadOnlyList<EditorToolbarButton> buttons, EditorToolbarAction action) =>
        buttons.Single(b => b.Action == action).Rect;

    [Fact]
    public void Build_lays_out_creation_left_aligned_and_actions_right_aligned()
    {
        var buttons = EditorToolbarButtons.Build(Toolbar, EditorChromeStrings.English, AllEnabled);

        buttons.Select(b => b.Action).Should().Equal(
            EditorToolbarAction.AddCube, EditorToolbarAction.AddSphere, EditorToolbarAction.AddCylinder,
            EditorToolbarAction.AddPlane, EditorToolbarAction.AddCone,
            EditorToolbarAction.AddNode, EditorToolbarAction.AddLight, EditorToolbarAction.AddModel,
            EditorToolbarAction.Save, EditorToolbarAction.Undo, EditorToolbarAction.Redo,
            EditorToolbarAction.Delete, EditorToolbarAction.Frame);
        buttons[0].Rect.X.Should().Be(Toolbar.X + EditorToolbarButtons.LeftPadding, "creation is left-aligned");
        buttons[^1].Rect.Right.Should().BeLessThanOrEqualTo(Toolbar.Right);
        RectOf(buttons, EditorToolbarAction.Save).X.Should().BeGreaterThan(
            Toolbar.Width / 2, "the action group is right-aligned");
    }

    [Fact]
    public void Title_text_starts_just_past_the_creation_group()
    {
        var buttons = EditorToolbarButtons.Build(Toolbar, EditorChromeStrings.English, AllEnabled);

        EditorToolbarButtons.TitleStartX(Toolbar).Should().Be(
            RectOf(buttons, EditorToolbarAction.AddModel).Right + EditorToolbarButtons.TitleGap);
    }

    [Fact]
    public void Save_is_enabled_only_when_the_document_is_dirty()
    {
        var dirty = EditorToolbarButtons.Build(Toolbar, EditorChromeStrings.English, AllEnabled);
        var clean = EditorToolbarButtons.Build(
            Toolbar, EditorChromeStrings.English, AllEnabled with { IsDirty = false });

        dirty.Single(b => b.Action == EditorToolbarAction.Save).Enabled.Should().BeTrue();
        clean.Single(b => b.Action == EditorToolbarAction.Save).Enabled.Should().BeFalse();
    }

    [Fact]
    public void Buttons_are_disabled_to_match_the_document_state()
    {
        var buttons = EditorToolbarButtons.Build(
            Toolbar,
            EditorChromeStrings.English,
            new EditorToolbarState(CanUndo: false, CanRedo: false, HasSelection: false, IsDirty: false));

        buttons.Where(IsCreation).Should().OnlyContain(b => b.Enabled, "creation needs no selection or history");
        buttons.Where(b => !IsCreation(b)).Should().OnlyContain(b => !b.Enabled);
    }

    private static bool IsCreation(EditorToolbarButton button) =>
        button.Action is EditorToolbarAction.AddNode or EditorToolbarAction.AddLight
            or EditorToolbarAction.AddCube or EditorToolbarAction.AddSphere or EditorToolbarAction.AddCylinder
            or EditorToolbarAction.AddPlane or EditorToolbarAction.AddCone or EditorToolbarAction.AddModel;

    [Fact]
    public void Hit_test_returns_the_enabled_action_under_the_pixel()
    {
        var buttons = EditorToolbarButtons.Build(Toolbar, EditorChromeStrings.English, AllEnabled);
        var undo = RectOf(buttons, EditorToolbarAction.Undo);

        EditorToolbarButtons.HitTest(buttons, undo.X + 1, undo.Y + 1).Should().Be(EditorToolbarAction.Undo);
    }

    [Fact]
    public void Hit_test_ignores_disabled_buttons()
    {
        var buttons = EditorToolbarButtons.Build(
            Toolbar,
            EditorChromeStrings.English,
            new EditorToolbarState(CanUndo: false, CanRedo: false, HasSelection: false, IsDirty: false));
        var undo = RectOf(buttons, EditorToolbarAction.Undo);

        EditorToolbarButtons.HitTest(buttons, undo.X + 1, undo.Y + 1).Should().Be(EditorToolbarAction.None);
    }

    [Fact]
    public void Hit_test_off_every_button_is_none()
    {
        var buttons = EditorToolbarButtons.Build(Toolbar, EditorChromeStrings.English, AllEnabled);

        EditorToolbarButtons.HitTest(buttons, 5, 5).Should().Be(EditorToolbarAction.None);
    }

    [Fact]
    public void A_narrow_toolbar_drops_trailing_creation_buttons_instead_of_overlapping()
    {
        var narrow = new EditorPanelRect(0, 0, 700, 32);

        var buttons = EditorToolbarButtons.Build(narrow, EditorChromeStrings.English, AllEnabled);

        int actionStart = EditorToolbarButtons.ActionGroupStartX(narrow);
        buttons.Where(IsCreation).Should().NotBeEmpty("the leading creation buttons still fit at 700 px");
        buttons.Where(IsCreation).Should().OnlyContain(
            b => b.Rect.Right < actionStart, "no creation button may slide under the action group");
        buttons.Where(b => !IsCreation(b)).Should().HaveCount(5, "the action group never loses a button");
    }

    [Fact]
    public void A_tiny_toolbar_keeps_only_the_action_group()
    {
        var tiny = new EditorPanelRect(0, 0, 420, 32);

        var buttons = EditorToolbarButtons.Build(tiny, EditorChromeStrings.English, AllEnabled);

        buttons.Where(IsCreation).Should().BeEmpty("there is no room left of the action group");
        buttons.Where(b => !IsCreation(b)).Should().HaveCount(5);
    }
}
