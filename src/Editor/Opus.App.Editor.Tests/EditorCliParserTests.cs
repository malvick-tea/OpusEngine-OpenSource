using System;
using FluentAssertions;
using Opus.App.Editor.Cli;
using Opus.Editor.Core;
using Opus.Editor.Ui;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorCliParserTests
{
    [Fact]
    public void No_args_opens_the_default_window()
    {
        var args = EditorCliParser.Parse(Array.Empty<string>());

        args.Mode.Should().Be(EditorMode.Window);
        args.ScenePath.Should().BeNull();
        args.HelpReason.Should().BeEmpty();
    }

    [Theory]
    [InlineData("new", EditorMode.New)]
    [InlineData("show", EditorMode.Show)]
    [InlineData("dsl", EditorMode.Dsl)]
    [InlineData("inspect", EditorMode.Inspect)]
    public void Scene_command_captures_path(string command, EditorMode mode)
    {
        var args = EditorCliParser.Parse(new[] { command, "harbor.scene.json" });

        args.Mode.Should().Be(mode);
        args.ScenePath.Should().Be("harbor.scene.json");
    }

    [Fact]
    public void New_captures_the_name_option()
    {
        var args = EditorCliParser.Parse(new[] { "new", "h.json", "--name", "Harbor" });

        args.SceneName.Should().Be("Harbor");
    }

    [Fact]
    public void Scene_command_without_a_path_is_help_with_a_reason()
    {
        var args = EditorCliParser.Parse(new[] { "show" });

        args.Mode.Should().Be(EditorMode.Help);
        args.HelpReason.Should().NotBeEmpty();
    }

    [Fact]
    public void Unknown_command_is_help_with_a_reason()
    {
        var args = EditorCliParser.Parse(new[] { "frobnicate" });

        args.Mode.Should().Be(EditorMode.Help);
        args.HelpReason.Should().Contain("frobnicate");
    }

    [Fact]
    public void Window_command_parses()
    {
        EditorCliParser.Parse(new[] { "window" }).Mode.Should().Be(EditorMode.Window);
    }

    [Fact]
    public void Window_captures_the_settings_file_option()
    {
        var args = EditorCliParser.Parse(new[] { "window", "harbor.scene.json", "--settings", "editor.settings.json" });

        args.Mode.Should().Be(EditorMode.Window);
        args.ScenePath.Should().Be("harbor.scene.json");
        args.SettingsPath.Should().Be("editor.settings.json");
    }

    [Fact]
    public void Window_without_a_settings_option_has_no_settings_path()
    {
        EditorCliParser.Parse(new[] { "window" }).SettingsPath.Should().BeNull();
    }

    [Fact]
    public void Window_without_a_lang_option_leaves_the_language_unset()
    {
        // Unset (null) lets the launch resolution fall back to the persisted language, else English; the
        // parser does not force a default so an explicit choice is distinguishable from "no choice given".
        EditorCliParser.Parse(new[] { "window" }).Language.Should().BeNull();
    }

    [Fact]
    public void Window_lang_ru_selects_russian_chrome()
    {
        EditorCliParser.Parse(new[] { "window", "--lang", "ru" }).Language.Should().Be(EditorLanguage.Russian);
    }

    [Fact]
    public void Window_with_an_unknown_lang_is_help_with_a_reason()
    {
        var args = EditorCliParser.Parse(new[] { "window", "--lang", "fr" });

        args.Mode.Should().Be(EditorMode.Help);
        args.HelpReason.Should().NotBeEmpty();
    }

    [Fact]
    public void Anim_remove_state_captures_graph_and_name()
    {
        var args = EditorCliParser.Parse(new[] { "anim-remove-state", "loco.animgraph.json", "Walk" });

        args.Mode.Should().Be(EditorMode.AnimRemoveState);
        args.ScenePath.Should().Be("loco.animgraph.json");
        args.Animation!.StateName.Should().Be("Walk");
    }

    [Fact]
    public void Anim_remove_transition_captures_from_to_and_trigger()
    {
        var args = EditorCliParser.Parse(
            new[] { "anim-remove-transition", "loco.animgraph.json", "Idle", "Walk", "--on", "move" });

        args.Mode.Should().Be(EditorMode.AnimRemoveTransition);
        args.Animation!.FromState.Should().Be("Idle");
        args.Animation.ToState.Should().Be("Walk");
        args.Animation.Trigger.Should().Be("move");
    }

    [Fact]
    public void Anim_remove_transition_without_a_trigger_is_help()
    {
        EditorCliParser.Parse(new[] { "anim-remove-transition", "g.json", "Idle", "Walk" })
            .Mode.Should().Be(EditorMode.Help);
    }

    [Fact]
    public void Place_captures_scene_asset_position_and_name()
    {
        var args = EditorCliParser.Parse(
            new[] { "place", "h.json", "models/tank.glb", "--at", "10,0,5", "--name", "tank_a" });

        args.Mode.Should().Be(EditorMode.Place);
        args.ScenePath.Should().Be("h.json");
        args.AssetRef.Should().Be("models/tank.glb");
        args.Position.Should().Be(new Float3(10f, 0f, 5f));
        args.SceneName.Should().Be("tank_a");
    }

    [Fact]
    public void Scene_duplicate_captures_node_id_and_optional_position()
    {
        var args = EditorCliParser.Parse(new[] { "scene-duplicate", "h.json", "3", "--at", "1,2,3" });

        args.Mode.Should().Be(EditorMode.SceneDuplicate);
        args.ScenePath.Should().Be("h.json");
        args.AssetRef.Should().Be("3");
        args.Position.Should().Be(new Float3(1f, 2f, 3f));
    }

    [Fact]
    public void Scene_duplicate_without_a_node_id_is_help()
    {
        EditorCliParser.Parse(new[] { "scene-duplicate", "h.json" }).Mode.Should().Be(EditorMode.Help);
    }

    [Fact]
    public void Scene_parent_captures_child_and_parent_ids()
    {
        var args = EditorCliParser.Parse(new[] { "scene-parent", "h.json", "2", "1" });

        args.Mode.Should().Be(EditorMode.SceneParent);
        args.ScenePath.Should().Be("h.json");
        args.AssetRef.Should().Be("2", "the child id rides AssetRef");
        args.SceneName.Should().Be("1", "the parent id rides SceneName");
    }

    [Fact]
    public void Scene_parent_without_a_parent_id_is_help()
    {
        EditorCliParser.Parse(new[] { "scene-parent", "h.json", "2" }).Mode.Should().Be(EditorMode.Help);
    }

    [Fact]
    public void Scene_unparent_captures_the_node_id()
    {
        var args = EditorCliParser.Parse(new[] { "scene-unparent", "h.json", "2" });

        args.Mode.Should().Be(EditorMode.SceneUnparent);
        args.AssetRef.Should().Be("2");
    }

    [Fact]
    public void Scene_rotate_captures_node_id_and_euler()
    {
        var args = EditorCliParser.Parse(new[] { "scene-rotate", "h.json", "2", "--euler", "0,90,0" });

        args.Mode.Should().Be(EditorMode.SceneRotate);
        args.AssetRef.Should().Be("2");
        args.Position.Should().Be(new Float3(0f, 90f, 0f));
    }

    [Fact]
    public void Scene_rotate_without_an_euler_is_help()
    {
        EditorCliParser.Parse(new[] { "scene-rotate", "h.json", "2" }).Mode.Should().Be(EditorMode.Help);
    }

    [Fact]
    public void Scene_scale_captures_node_id_and_scale()
    {
        var args = EditorCliParser.Parse(new[] { "scene-scale", "h.json", "2", "--scale", "2,2,2" });

        args.Mode.Should().Be(EditorMode.SceneScale);
        args.AssetRef.Should().Be("2");
        args.Position.Should().Be(new Float3(2f, 2f, 2f));
    }

    [Fact]
    public void Scene_scale_with_a_zero_component_is_help()
    {
        EditorCliParser.Parse(new[] { "scene-scale", "h.json", "2", "--scale", "1,0,1" }).Mode.Should().Be(EditorMode.Help);
    }

    [Fact]
    public void Place_without_an_asset_is_help()
    {
        EditorCliParser.Parse(new[] { "place", "h.json" }).Mode.Should().Be(EditorMode.Help);
    }

    [Fact]
    public void Place_with_a_malformed_at_option_is_help()
    {
        EditorCliParser.Parse(new[] { "place", "h.json", "m.glb", "--at", "nope" }).Mode.Should().Be(EditorMode.Help);
    }

    [Fact]
    public void Light_add_captures_kind_and_options()
    {
        var args = EditorCliParser.Parse(new[]
        {
            "light-add", "h.json", "point", "--name", "lamp", "--color", "1,0.8,0.6",
            "--intensity", "2", "--at", "3,4,0", "--range", "12",
        });

        args.Mode.Should().Be(EditorMode.LightAdd);
        args.ScenePath.Should().Be("h.json");
        args.SceneName.Should().Be("lamp");
        args.Light!.Kind.Should().Be(SceneLightKind.Point);
        args.Light.Color.Should().Be(new Float3(1f, 0.8f, 0.6f));
        args.Light.Intensity.Should().Be(2f);
        args.Light.Range.Should().Be(12f);
        args.Position.Should().Be(new Float3(3f, 4f, 0f));
    }

    [Fact]
    public void Light_add_spot_captures_the_cone()
    {
        var args = EditorCliParser.Parse(new[] { "light-add", "h.json", "spot", "--cone", "20,35" });

        args.Light!.Kind.Should().Be(SceneLightKind.Spot);
        args.Light.ConeInnerAngleDegrees.Should().Be(20f);
        args.Light.ConeOuterAngleDegrees.Should().Be(35f);
    }

    [Fact]
    public void Light_add_with_an_unknown_kind_is_help()
    {
        var args = EditorCliParser.Parse(new[] { "light-add", "h.json", "laser" });

        args.Mode.Should().Be(EditorMode.Help);
        args.HelpReason.Should().NotBeEmpty();
    }

    [Fact]
    public void Light_add_without_a_kind_is_help()
    {
        EditorCliParser.Parse(new[] { "light-add", "h.json" }).Mode.Should().Be(EditorMode.Help);
    }

    [Fact]
    public void Light_add_with_a_malformed_color_is_help()
    {
        EditorCliParser.Parse(new[] { "light-add", "h.json", "point", "--color", "white" })
            .Mode.Should().Be(EditorMode.Help);
    }

    [Fact]
    public void Light_add_with_a_malformed_cone_is_help()
    {
        EditorCliParser.Parse(new[] { "light-add", "h.json", "spot", "--cone", "20" })
            .Mode.Should().Be(EditorMode.Help);
    }

    [Fact]
    public void Light_remove_captures_the_light_id()
    {
        var args = EditorCliParser.Parse(new[] { "light-remove", "h.json", "2" });

        args.Mode.Should().Be(EditorMode.LightRemove);
        args.ScenePath.Should().Be("h.json");
        args.AssetRef.Should().Be("2");
    }

    [Fact]
    public void Light_remove_without_an_id_is_help()
    {
        EditorCliParser.Parse(new[] { "light-remove", "h.json" }).Mode.Should().Be(EditorMode.Help);
    }

    [Fact]
    public void Light_edit_captures_id_and_options_and_leaves_the_kind_unset()
    {
        var args = EditorCliParser.Parse(new[] { "light-edit", "h.json", "2", "--intensity", "4", "--range", "25" });

        args.Mode.Should().Be(EditorMode.LightEdit);
        args.AssetRef.Should().Be("2");
        args.Light!.Kind.Should().BeNull("light-edit keeps the existing light's kind");
        args.Light.Intensity.Should().Be(4f);
        args.Light.Range.Should().Be(25f);
    }

    [Fact]
    public void Light_edit_without_an_id_is_help()
    {
        EditorCliParser.Parse(new[] { "light-edit", "h.json" }).Mode.Should().Be(EditorMode.Help);
    }
}
