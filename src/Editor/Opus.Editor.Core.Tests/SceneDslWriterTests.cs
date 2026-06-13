using FluentAssertions;
using Xunit;

namespace Opus.Editor.Core.Tests;

public sealed class SceneDslWriterTests
{
    [Fact]
    public void Writes_a_node_with_asset_and_transform()
    {
        var document = new EditorSceneDocument(
            "Harbor",
            new[]
            {
                new SceneNode(
                    new SceneNodeId(1),
                    "tank_alpha",
                    "models/tank.glb",
                    new EditorTransform(new Float3(10f, 0f, 5f), new Float3(0f, 90f, 0f), Float3.One)),
            });

        var dsl = SceneDslWriter.Write(document);

        const string expected =
            "scene \"Harbor\" {\n" +
            "  node \"tank_alpha\" {\n" +
            "    id 1\n" +
            "    asset \"models/tank.glb\"\n" +
            "    position (10, 0, 5)\n" +
            "    rotation (0, 90, 0)\n" +
            "    scale (1, 1, 1)\n" +
            "  }\n" +
            "}\n";
        dsl.Should().Be(expected);
    }

    [Fact]
    public void A_hidden_node_and_light_print_the_hidden_marker_and_visible_ones_do_not()
    {
        var document = new EditorSceneDocument(
            "s",
            new[]
            {
                new SceneNode(new SceneNodeId(1), "terrain", null, EditorTransform.Identity).WithHidden(true),
            })
        {
            Lights = new[] { SceneLight.CreatePoint("torch").WithId(new SceneLightId(1)).WithHidden(true) },
        };

        var dsl = SceneDslWriter.Write(document);

        dsl.Should().Contain("node \"terrain\" {\n    id 1\n    hidden true\n");
        dsl.Should().Contain("light \"torch\" {\n    id 1\n    hidden true\n");

        var visible = new EditorSceneDocument(
            "s",
            new[] { new SceneNode(new SceneNodeId(1), "terrain", null, EditorTransform.Identity) });
        SceneDslWriter.Write(visible).Should().NotContain(
            "hidden", "a mirror without hidden elements is byte-identical to before");
    }

    [Fact]
    public void Writes_an_empty_scene()
    {
        var dsl = SceneDslWriter.Write(EditorSceneDocument.Empty("untitled"));

        dsl.Should().Be("scene \"untitled\" {\n}\n");
    }

    [Fact]
    public void Omits_the_asset_line_for_a_grouping_node()
    {
        var document = new EditorSceneDocument(
            "s",
            new[] { new SceneNode(new SceneNodeId(1), "group", null, EditorTransform.Identity) });

        var dsl = SceneDslWriter.Write(document);

        dsl.Should().NotContain("asset");
        dsl.Should().Contain("node \"group\" {");
    }

    [Fact]
    public void Formats_fractional_components_with_trimmed_precision()
    {
        var document = new EditorSceneDocument(
            "s",
            new[]
            {
                new SceneNode(
                    new SceneNodeId(1),
                    "n",
                    null,
                    new EditorTransform(new Float3(0.5f, -0.25f, 1.5f), Float3.Zero, Float3.One)),
            });

        SceneDslWriter.Write(document).Should().Contain("position (0.5, -0.25, 1.5)");
    }

    [Fact]
    public void Escapes_quotes_in_names()
    {
        var document = new EditorSceneDocument(
            "s",
            new[] { new SceneNode(new SceneNodeId(1), "a\"b", null, EditorTransform.Identity) });

        SceneDslWriter.Write(document).Should().Contain("node \"a\\\"b\" {");
    }

    [Fact]
    public void Writes_a_directional_light_with_only_its_relevant_fields()
    {
        var document = SceneWithLight(SceneLight.CreateDirectional("sun").WithId(new SceneLightId(1)));

        const string expected =
            "scene \"s\" {\n" +
            "  light \"sun\" {\n" +
            "    id 1\n" +
            "    kind directional\n" +
            "    color (1, 1, 1)\n" +
            "    intensity 1\n" +
            "    direction (0, -1, 0)\n" +
            "  }\n" +
            "}\n";
        SceneDslWriter.Write(document).Should().Be(expected);
    }

    [Fact]
    public void Writes_a_point_light_with_position_and_range_but_no_direction_or_cone()
    {
        var light = SceneLight.CreatePoint("lamp").WithId(new SceneLightId(2))
            with { Position = new Float3(3f, 4f, 0f), Range = 12f };
        var dsl = SceneDslWriter.Write(SceneWithLight(light));

        dsl.Should().Contain("kind point");
        dsl.Should().Contain("position (3, 4, 0)");
        dsl.Should().Contain("range 12");
        dsl.Should().NotContain("direction");
        dsl.Should().NotContain("cone");
    }

    [Fact]
    public void Writes_a_spot_light_with_position_direction_range_and_cone()
    {
        var light = SceneLight.CreateSpot("torch").WithId(new SceneLightId(3));
        var dsl = SceneDslWriter.Write(SceneWithLight(light));

        dsl.Should().Contain("kind spot");
        dsl.Should().Contain("position (0, 0, 0)");
        dsl.Should().Contain("direction (0, -1, 0)");
        dsl.Should().Contain("range 10");
        dsl.Should().Contain("cone (20, 30)");
    }

    [Fact]
    public void Lights_follow_nodes_in_the_mirror()
    {
        var document = new EditorSceneDocument(
            "s",
            new[] { new SceneNode(new SceneNodeId(1), "n", null, EditorTransform.Identity) })
        {
            Lights = new[] { SceneLight.CreateDirectional("sun").WithId(new SceneLightId(1)) },
        };

        var dsl = SceneDslWriter.Write(document);

        dsl.IndexOf("node \"n\"", System.StringComparison.Ordinal)
            .Should().BeLessThan(dsl.IndexOf("light \"sun\"", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Children_nest_inside_their_parent_block()
    {
        var document = new EditorSceneDocument(
            "Harbor",
            new[]
            {
                new SceneNode(new SceneNodeId(1), "group", null, EditorTransform.Identity),
                new SceneNode(new SceneNodeId(2), "box", null, EditorTransform.Identity)
                {
                    ParentId = new SceneNodeId(1),
                },
            });

        var dsl = SceneDslWriter.Write(document);

        const string expected =
            "scene \"Harbor\" {\n" +
            "  node \"group\" {\n" +
            "    id 1\n" +
            "    position (0, 0, 0)\n" +
            "    rotation (0, 0, 0)\n" +
            "    scale (1, 1, 1)\n" +
            "    node \"box\" {\n" +
            "      id 2\n" +
            "      position (0, 0, 0)\n" +
            "      rotation (0, 0, 0)\n" +
            "      scale (1, 1, 1)\n" +
            "    }\n" +
            "  }\n" +
            "}\n";
        dsl.Should().Be(expected);
    }

    [Fact]
    public void A_flat_scene_mirror_is_byte_identical_to_an_unnested_walk()
    {
        // No node carries a parent: every node is a root printed at depth 1, so hierarchy support must not
        // change a single byte of the mirror for the common flat case.
        var document = new EditorSceneDocument(
            "s",
            new[]
            {
                new SceneNode(new SceneNodeId(1), "a", "m.glb", EditorTransform.Identity),
                new SceneNode(new SceneNodeId(2), "b", null, EditorTransform.Identity),
            });

        SceneDslWriter.Write(document).Should().Be(
            "scene \"s\" {\n" +
            "  node \"a\" {\n" +
            "    id 1\n" +
            "    asset \"m.glb\"\n" +
            "    position (0, 0, 0)\n" +
            "    rotation (0, 0, 0)\n" +
            "    scale (1, 1, 1)\n" +
            "  }\n" +
            "  node \"b\" {\n" +
            "    id 2\n" +
            "    position (0, 0, 0)\n" +
            "    rotation (0, 0, 0)\n" +
            "    scale (1, 1, 1)\n" +
            "  }\n" +
            "}\n");
    }

    private static EditorSceneDocument SceneWithLight(SceneLight light) =>
        EditorSceneDocument.Empty("s") with { Lights = new[] { light } };
}
