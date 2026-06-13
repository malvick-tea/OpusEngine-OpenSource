using System;
using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class MarqueeSelectTests
{
    private const int Width = 800;
    private const int Height = 600;

    private static Matrix4x4 LookDownZ()
    {
        // An eye on +Z looking at the origin: world X maps left/right, world Y up/down, so the
        // expected screen positions are easy to reason about.
        var view = Matrix4x4.CreateLookAt(new Vector3(0f, 0f, 10f), Vector3.Zero, Vector3.UnitY);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI / 3f, Width / (float)Height, 0.1f, 100f);
        return view * projection;
    }

    [Fact]
    public void Collects_the_node_and_light_anchored_inside_the_rectangle()
    {
        var document = new EditorDocument("scene");
        var node = document.PlaceNode("centre", null, EditorTransform.Identity);
        var lamp = document.AddNewPointLight(Float3.Zero);

        var inside = MarqueeSelect.Collect(
            document.Scene, LookDownZ(), Width, Height,
            new Vector2(300f, 200f), new Vector2(500f, 400f));

        inside.Should().Equal(SceneElementRef.Node(node), SceneElementRef.Light(lamp));
    }

    [Fact]
    public void Skips_elements_anchored_outside_the_rectangle()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode(
            "far-left", null, EditorTransform.Identity with { Position = new Float3(-8f, 0f, 0f) });
        var centre = document.PlaceNode("centre", null, EditorTransform.Identity);

        var inside = MarqueeSelect.Collect(
            document.Scene, LookDownZ(), Width, Height,
            new Vector2(300f, 200f), new Vector2(500f, 400f));

        inside.Should().Equal(SceneElementRef.Node(centre));
    }

    [Fact]
    public void Never_collects_hidden_elements()
    {
        var document = new EditorDocument("scene");
        var node = document.PlaceNode("centre", null, EditorTransform.Identity);
        var lamp = document.AddNewPointLight(Float3.Zero);
        document.SetNodeHidden(node, true);
        document.SetLight(document.Scene.FindLight(lamp)! with { Hidden = true });

        var inside = MarqueeSelect.Collect(
            document.Scene, LookDownZ(), Width, Height,
            new Vector2(0f, 0f), new Vector2(Width, Height));

        inside.Should().BeEmpty("what is not drawn is not selectable");
    }

    [Fact]
    public void Never_collects_an_anchor_behind_the_camera()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode(
            "behind", null, EditorTransform.Identity with { Position = new Float3(0f, 0f, 20f) });

        var inside = MarqueeSelect.Collect(
            document.Scene, LookDownZ(), Width, Height,
            new Vector2(0f, 0f), new Vector2(Width, Height));

        inside.Should().BeEmpty();
    }
}
