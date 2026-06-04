using System.Numerics;
using System.Runtime.InteropServices;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>One record in the per-frame instance <c>StructuredBuffer</c> read by the
/// instanced forward vertex shader (root SRV <c>t1</c>) at <c>SV_InstanceID + InstanceOffset</c>.
/// 88 bytes: a row-major world matrix + the per-instance albedo tint and UV offset. Matches the HLSL
/// <c>struct InstanceData</c> in <see cref="ForwardSceneShaders.SceneVertexShader"/>; the
/// tank/shell instances of one mesh are uploaded as a contiguous slice and fanned by a single
/// <c>DrawIndexedInstanced</c> per primitive.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct GpuInstanceData
{
    /// <summary>Byte size of one record — the HLSL <c>StructuredBuffer&lt;InstanceData&gt;</c>
    /// stride. The CPU array is uploaded tightly packed at this stride.</summary>
    public const int SizeBytes = 88;

    /// <summary>Per-instance world transform (row-major, same convention as
    /// <see cref="ForwardSceneConstants.ViewProjection"/> and <c>Aabb.Transform</c>).</summary>
    public Matrix4x4 World;

    /// <summary>Per-instance albedo tint; multiplied in the vertex shader by the per-primitive
    /// material factor (root constant) to reproduce the pre-instancing
    /// <c>factor * draw.TintFactor</c> shading exactly.</summary>
    public Vector4 BaseColorFactor;

    /// <summary>Per-instance UV translation used by animated surfaces such as tank tracks.</summary>
    public Vector2 UvOffset;
}
