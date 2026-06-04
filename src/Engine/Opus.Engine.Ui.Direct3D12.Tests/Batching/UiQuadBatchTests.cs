using System.Numerics;
using FluentAssertions;
using Opus.Engine.Ui.Direct3D12.Batching;
using Xunit;

namespace Opus.Engine.Ui.Direct3D12.Tests.Batching;

public sealed class UiQuadBatchTests
{
    [Fact]
    public void Append_quad_grows_backing_store_without_dropping_vertices()
    {
        var batch = new UiQuadBatch(initialQuadCapacity: 1);
        var vertex = new UiQuadVertex(Vector2.Zero, Vector2.Zero, 0u, UiDrawMode.Textured, Vector2.Zero);

        batch.AppendQuad(vertex, vertex, vertex, vertex);
        batch.AppendQuad(vertex, vertex, vertex, vertex);

        batch.QuadCount.Should().Be(2);
        batch.VertexCount.Should().Be(12);
    }

    [Fact]
    public void Clear_keeps_batch_reusable()
    {
        var batch = new UiQuadBatch(initialQuadCapacity: 1);
        var vertex = new UiQuadVertex(Vector2.Zero, Vector2.Zero, 0u, UiDrawMode.Textured, Vector2.Zero);

        batch.AppendQuad(vertex, vertex, vertex, vertex);
        batch.Clear();
        batch.AppendQuad(vertex, vertex, vertex, vertex);

        batch.QuadCount.Should().Be(1);
        batch.IsEmpty.Should().BeFalse();
    }
}
