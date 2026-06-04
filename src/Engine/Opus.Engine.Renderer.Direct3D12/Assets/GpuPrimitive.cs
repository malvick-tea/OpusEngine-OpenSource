using Opus.Engine.Rhi.Direct3D12;

namespace Opus.Engine.Renderer.Direct3D12.Assets;

/// <summary>One uploaded primitive ready to draw: vertex buffer + index buffer + index
/// count, plus the source glTF material index (null when the primitive declared no
/// material). The renderer disposes both buffers via the surrounding
/// <see cref="GpuScene"/> when the scene tears down.</summary>
public sealed record GpuPrimitive(
    D3D12Buffer Vb,
    D3D12Buffer Ib,
    uint IndexCount,
    int? MaterialIndex);

/// <summary>Range of primitive indices in the flat <see cref="GpuScene.Primitives"/>
/// array that belong to one glTF mesh: <c>Start..Start+Count</c> exclusive. Render
/// loops iterate the slice when a node references that mesh.</summary>
public readonly record struct GpuMeshSlice(int Start, int Count);

/// <summary>Result of uploading every primitive of every glTF mesh to its own VB+IB
/// pair. <see cref="Primitives"/> is the flat per-primitive list (deterministic order:
/// mesh 0 primitive 0, then mesh 0 primitive 1, …). <see cref="SlicesByMesh"/> maps a
/// glTF mesh index back to its slice of the flat list so a render pass can resolve
/// <c>node.MeshIndex → primitive range → draw calls</c>.</summary>
public sealed record GpuScene(GpuPrimitive[] Primitives, GpuMeshSlice[] SlicesByMesh);
