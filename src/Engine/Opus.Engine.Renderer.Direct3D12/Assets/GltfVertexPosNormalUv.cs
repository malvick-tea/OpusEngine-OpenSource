using System.Numerics;
using System.Runtime.InteropServices;

namespace Opus.Engine.Renderer.Direct3D12.Assets;

/// <summary>Vertex format used by the glTF-loading path: 32-byte interleaved POS + NORMAL
/// + UV. Matches the input layout of
/// <c>D3D12GraphicsPipelineFactory.CreatePosNormalUvLitDepth</c> — every sample-vehicle mesh
/// loaded by <see cref="D3D12GltfSceneLoader"/> packs into this layout, then renders
/// through any PSO built on that input signature.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct GltfVertexPosNormalUv
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 Uv;
}
