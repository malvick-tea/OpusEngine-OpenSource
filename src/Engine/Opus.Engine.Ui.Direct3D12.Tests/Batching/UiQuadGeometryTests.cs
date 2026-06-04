using System.Numerics;
using FluentAssertions;
using Opus.Engine.Ui;
using Opus.Engine.Ui.Direct3D12.Batching;
using Xunit;

namespace Opus.Engine.Ui.Direct3D12.Tests.Batching;

public sealed class UiQuadGeometryTests
{
    private static readonly Vector2 WhiteUv = new(0.5f, 0.25f);

    [Fact]
    public void Rect_emits_one_textured_quad()
    {
        var batch = new UiQuadBatch(initialQuadCapacity: 1);
        var color = new Color(10, 20, 30, 40);

        UiQuadGeometry.Rect(batch, 2, 4, 8, 6, color, WhiteUv);

        batch.QuadCount.Should().Be(1);
        batch.VertexCount.Should().Be(6);
        var vertices = batch.Vertices.ToArray();
        vertices[0].Position.Should().Be(new Vector2(2, 4));
        vertices[1].Position.Should().Be(new Vector2(10, 4));
        vertices[2].Position.Should().Be(new Vector2(10, 10));
        vertices[5].Position.Should().Be(new Vector2(2, 10));
        vertices.Should().OnlyContain(v => v.Uv == WhiteUv);
        vertices.Should().OnlyContain(v => v.Rgba == UiQuadVertex.PackColor(color));
        vertices.Should().OnlyContain(v => v.Mode == (float)UiDrawMode.Textured);
    }

    [Fact]
    public void Line_ignores_zero_length_segments()
    {
        var batch = new UiQuadBatch(initialQuadCapacity: 1);

        UiQuadGeometry.Line(batch, 3, 3, 3, 3, 4, Color.White, WhiteUv);

        batch.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Circle_emits_analytic_shape_quad()
    {
        var batch = new UiQuadBatch(initialQuadCapacity: 1);

        UiQuadGeometry.Circle(batch, centreX: 20, centreY: 30, radius: 10, Color.White);

        batch.QuadCount.Should().Be(1);
        var vertices = batch.Vertices.ToArray();
        vertices[0].Position.Should().Be(new Vector2(10, 20));
        vertices[2].Position.Should().Be(new Vector2(30, 40));
        vertices.Should().OnlyContain(v => v.Mode == (float)UiDrawMode.FilledCircle);
        vertices.Should().OnlyContain(v => v.ShapeParams.X > 0f);
    }

    [Fact]
    public void Textured_rect_uses_full_rgba_uvs()
    {
        var batch = new UiQuadBatch(initialQuadCapacity: 1);

        UiQuadGeometry.TexturedRectRgba(batch, 1, 2, 3, 4, Color.White);

        batch.QuadCount.Should().Be(1);
        var vertices = batch.Vertices.ToArray();
        vertices[0].Uv.Should().Be(new Vector2(0, 0));
        vertices[1].Uv.Should().Be(new Vector2(1, 0));
        vertices[2].Uv.Should().Be(new Vector2(1, 1));
        vertices[5].Uv.Should().Be(new Vector2(0, 1));
        vertices.Should().OnlyContain(v => v.Mode == (float)UiDrawMode.TexturedRgba);
    }
}
