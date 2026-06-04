using System.Numerics;
using System.Runtime.InteropServices;
using Opus.Engine.Ui;

namespace Opus.Engine.Ui.Direct3D12.Batching;

/// <summary>
/// One vertex of the 2D UI quad stream. Layout is fixed by
/// <c>D3D12GraphicsPipelineFactory.CreateUiSprite</c>: position(float2) + uv(float2) +
/// rgba(uint, 4×unorm8) + mode(float) + shape params(float2) — 32 bytes, the stride
/// reported as <c>UiSpriteVertexStride</c>.
/// </summary>
/// <remarks>
/// <see cref="Position"/> is in pixels (the vertex shader maps it to clip space).
/// <see cref="Uv"/> is an atlas coordinate for <see cref="UiDrawMode.Textured"/> quads
/// and the quad-local 0..1 coordinate for the analytic shape modes.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct UiQuadVertex
{
    public readonly Vector2 Position;
    public readonly Vector2 Uv;
    public readonly uint Rgba;
    public readonly float Mode;
    public readonly Vector2 ShapeParams;

    public UiQuadVertex(Vector2 position, Vector2 uv, uint rgba, UiDrawMode mode, Vector2 shapeParams)
    {
        Position = position;
        Uv = uv;
        Rgba = rgba;
        Mode = (float)(int)mode;
        ShapeParams = shapeParams;
    }

    /// <summary>Packs an engine <see cref="Color"/> into the little-endian RGBA byte order
    /// the <c>R8G8B8A8_UNORM</c> input element expects (byte 0 = red).</summary>
    public static uint PackColor(Color color) =>
        color.R | ((uint)color.G << 8) | ((uint)color.B << 16) | ((uint)color.A << 24);
}
