using System;

namespace Opus.Engine.Ui.Direct3D12.Batching;

/// <summary>
/// CPU-side accumulator for one frame of UI quads. Every <see cref="IDrawSurface"/>
/// primitive expands to one or more quads; the draw surface appends them here during a
/// frame and uploads <see cref="Vertices"/> to a GPU vertex buffer in a single flush.
/// </summary>
/// <remarks>
/// Each quad becomes six vertices (two triangles) so the whole frame draws with one
/// non-indexed <c>DrawInstanced</c>. The backing array grows by doubling; it is never
/// shrunk, so a batch reused across frames settles at the worst-case frame's size.
/// Pure and GPU-free — the upload is the draw surface's responsibility.
/// </remarks>
internal sealed class UiQuadBatch
{
    private const int VerticesPerQuad = 6;

    private UiQuadVertex[] _vertices;
    private int _count;

    public UiQuadBatch(int initialQuadCapacity)
    {
        if (initialQuadCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(initialQuadCapacity), "Capacity must be >= 1.");
        }

        _vertices = new UiQuadVertex[initialQuadCapacity * VerticesPerQuad];
    }

    public int VertexCount => _count;

    public int QuadCount => _count / VerticesPerQuad;

    public ReadOnlySpan<UiQuadVertex> Vertices => _vertices.AsSpan(0, _count);

    public bool IsEmpty => _count == 0;

    /// <summary>Drops every queued vertex without releasing the backing array — call once
    /// per frame before the screen renders.</summary>
    public void Clear() => _count = 0;

    /// <summary>Appends one axis-agnostic quad as two triangles. Corners are given in
    /// top-left, top-right, bottom-right, bottom-left order; the sprite pipeline is
    /// cull-none, so the winding only has to be consistent, not a particular handedness.</summary>
    public void AppendQuad(in UiQuadVertex topLeft, in UiQuadVertex topRight, in UiQuadVertex bottomRight, in UiQuadVertex bottomLeft)
    {
        EnsureCapacity(_count + VerticesPerQuad);
        _vertices[_count++] = topLeft;
        _vertices[_count++] = topRight;
        _vertices[_count++] = bottomRight;
        _vertices[_count++] = topLeft;
        _vertices[_count++] = bottomRight;
        _vertices[_count++] = bottomLeft;
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _vertices.Length)
        {
            return;
        }

        var grown = _vertices.Length * 2;
        while (grown < required)
        {
            grown *= 2;
        }

        Array.Resize(ref _vertices, grown);
    }
}
