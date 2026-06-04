namespace Opus.Engine.Ui.Direct3D12.Batching;

/// <summary>
/// Per-quad shading branch for the 2D sprite pixel shader. The numeric values are part
/// of the GPU contract — they are written into the quad vertex stream and compared in
/// <c>Sprite2D.hlsl</c>'s <c>PsMain</c>. Keep the values and the shader branches in lock-step.
/// </summary>
internal enum UiDrawMode
{
    /// <summary>Sample the glyph atlas at the quad's UV. Backs rects, lines, stroke
    /// outlines and glyph runs — solid fills point their UV at the atlas white texel.</summary>
    Textured = 0,

    /// <summary>Analytic anti-aliased filled disc. UV is the quad-local 0..1 coordinate;
    /// shape param X is the edge feather in normalised-radius units.</summary>
    FilledCircle = 1,

    /// <summary>Analytic anti-aliased annulus. Shape param X is the feather, param Y is
    /// the inner radius as a fraction of the outer radius.</summary>
    Ring = 2,

    /// <summary>Sample a full RGBA texture (not the single-channel coverage atlas) at the
    /// quad's UV and modulate by the vertex colour. Backs <see cref="D3D12DrawSurface.DrawTexturedRect"/>
    /// — used to composite externally-rendered targets (e.g. an offscreen scene viewport)
    /// into the UI quad batch. Bind the source SRV via the descriptor table that the draw
    /// surface flushes for this segment.</summary>
    TexturedRgba = 3,
}
