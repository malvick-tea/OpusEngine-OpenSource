using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Opus.Engine.Ui;
using Opus.Engine.Ui.Direct3D12.Batching;
using Xunit;

namespace Opus.Engine.Ui.Direct3D12.Tests.Ink;

/// <summary>Headless coverage for <see cref="UiInkTessellator"/> — the pure expansion of an
/// <see cref="InkStroke"/> into <see cref="UiQuadBatch"/> quads (one swept rectangle per segment +
/// one round disc per point). No GPU is touched.</summary>
public sealed class UiInkTessellatorTests
{
    private static readonly Vector2 WhiteUv = new(0.5f, 0.25f);
    private static readonly Color Ink = Color.FromRgb(220, 40, 40);

    [Fact]
    public void Empty_stroke_emits_nothing()
    {
        var batch = new UiQuadBatch(initialQuadCapacity: 1);

        UiInkTessellator.Append(batch, new InkStroke(new List<Vector2>(), 6f, Ink), WhiteUv);

        batch.QuadCount.Should().Be(0);
    }

    [Fact]
    public void Zero_width_stroke_emits_nothing()
    {
        var batch = new UiQuadBatch(initialQuadCapacity: 1);
        var points = new List<Vector2> { new(0, 0), new(10, 0) };

        UiInkTessellator.Append(batch, new InkStroke(points, 0f, Ink), WhiteUv);

        batch.QuadCount.Should().Be(0);
    }

    [Fact]
    public void Single_point_stroke_emits_one_round_disc()
    {
        var batch = new UiQuadBatch(initialQuadCapacity: 1);

        UiInkTessellator.Append(batch, new InkStroke(new List<Vector2> { new(50, 50) }, 8f, Ink), WhiteUv);

        batch.QuadCount.Should().Be(1);
        var verts = batch.Vertices.ToArray();
        verts.Should().OnlyContain(v => v.Mode == (float)UiDrawMode.FilledCircle);
        // The disc quad is the point's bounding box at radius = width / 2.
        verts.Min(v => v.Position.X).Should().Be(46f);
        verts.Max(v => v.Position.X).Should().Be(54f);
        verts.Min(v => v.Position.Y).Should().Be(46f);
        verts.Max(v => v.Position.Y).Should().Be(54f);
    }

    [Fact]
    public void Two_point_stroke_emits_one_segment_and_two_caps()
    {
        var batch = new UiQuadBatch(initialQuadCapacity: 4);
        var points = new List<Vector2> { new(10, 10), new(40, 10) };

        UiInkTessellator.Append(batch, new InkStroke(points, 6f, Ink), WhiteUv);

        batch.QuadCount.Should().Be(3, "one swept segment + a round cap disc at each end");
        var verts = batch.Vertices.ToArray();
        verts.Count(v => v.Mode == (float)UiDrawMode.Textured).Should().Be(6, "one segment quad");
        verts.Count(v => v.Mode == (float)UiDrawMode.FilledCircle).Should().Be(12, "two cap discs");
    }

    [Fact]
    public void Polyline_emits_one_segment_per_edge_and_one_disc_per_point()
    {
        var batch = new UiQuadBatch(initialQuadCapacity: 8);
        var points = new List<Vector2> { new(0, 0), new(30, 30), new(60, 0), new(90, 30) };

        UiInkTessellator.Append(batch, new InkStroke(points, 5f, Ink), WhiteUv);

        // 4 points -> 3 segments + 4 join/cap discs = 7 quads.
        batch.QuadCount.Should().Be(7);
        var verts = batch.Vertices.ToArray();
        verts.Count(v => v.Mode == (float)UiDrawMode.Textured).Should().Be(18, "three segment quads");
        verts.Count(v => v.Mode == (float)UiDrawMode.FilledCircle).Should().Be(24, "four discs");
    }

    [Fact]
    public void Segment_quads_carry_the_stroke_colour()
    {
        var batch = new UiQuadBatch(initialQuadCapacity: 4);
        var points = new List<Vector2> { new(10, 10), new(40, 10) };

        UiInkTessellator.Append(batch, new InkStroke(points, 6f, Ink), WhiteUv);

        var expected = UiQuadVertex.PackColor(Ink);
        batch.Vertices.ToArray().Should().OnlyContain(v => v.Rgba == expected);
    }
}
