using System;
using System.Runtime.InteropServices;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;

namespace Opus.Engine.Renderer.Direct3D12.Assets;

/// <summary>Uploads the procedural <see cref="FloorMesh"/> to a fresh VB + IB pair and
/// returns a <see cref="GpuPrimitive"/> ready to participate in a <see cref="GpuScene"/>.
/// The primitive declares <c>MaterialIndex = null</c> so atlases that key off material
/// index fall through to their default slot — at this milestone the floor renders with
/// whichever single albedo the <see cref="SingleTextureAtlas"/> already binds.</summary>
public static class FloorPrimitiveUploader
{
    public static GpuPrimitive Upload(
        D3D12RhiDevice device,
        string namePrefix,
        float halfExtentMeters = FloorMesh.DefaultHalfExtentMeters)
    {
        ArgumentNullException.ThrowIfNull(device);

        var verts = FloorMesh.BuildVertices(halfExtentMeters);
        var indices = FloorMesh.BuildIndices();

        var vb = device.CreateGraphicsBuffer(new RhiBufferDescription(
            $"{namePrefix}.floor.verts",
            verts.Length * Marshal.SizeOf<GltfVertexPosNormalUv>(),
            RhiBufferUsage.Vertex));
        BufferUploadHelper.WriteStructs(vb, verts);

        var ib = device.CreateGraphicsBuffer(new RhiBufferDescription(
            $"{namePrefix}.floor.indices",
            indices.Length * sizeof(uint),
            RhiBufferUsage.Index));
        BufferUploadHelper.WriteStructs(ib, indices);

        return new GpuPrimitive(vb, ib, (uint)indices.Length, MaterialIndex: null);
    }
}
