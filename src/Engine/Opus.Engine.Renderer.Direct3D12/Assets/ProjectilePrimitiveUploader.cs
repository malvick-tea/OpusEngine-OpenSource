using System;
using System.Runtime.InteropServices;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;

namespace Opus.Engine.Renderer.Direct3D12.Assets;

/// <summary>Uploads the procedural <see cref="ProjectileMesh"/> cube to a fresh VB + IB
/// pair and returns a <see cref="GpuPrimitive"/>. Declared with <c>MaterialIndex = 0</c>
/// (any non-null index works) so the atlas resolves to its main factor — projectiles
/// render with the tank albedo modulator, distinct from the floor which uses the
/// null-material factor.</summary>
public static class ProjectilePrimitiveUploader
{
    public static GpuPrimitive Upload(
        D3D12RhiDevice device,
        string namePrefix,
        float halfExtentMeters = ProjectileMesh.DefaultHalfExtentMeters)
    {
        ArgumentNullException.ThrowIfNull(device);

        var verts = ProjectileMesh.BuildVertices(halfExtentMeters);
        var indices = ProjectileMesh.BuildIndices();

        var vb = device.CreateGraphicsBuffer(new RhiBufferDescription(
            $"{namePrefix}.projectile.verts",
            verts.Length * Marshal.SizeOf<GltfVertexPosNormalUv>(),
            RhiBufferUsage.Vertex));
        BufferUploadHelper.WriteStructs(vb, verts);

        var ib = device.CreateGraphicsBuffer(new RhiBufferDescription(
            $"{namePrefix}.projectile.indices",
            indices.Length * sizeof(uint),
            RhiBufferUsage.Index));
        BufferUploadHelper.WriteStructs(ib, indices);

        return new GpuPrimitive(vb, ib, (uint)indices.Length, MaterialIndex: 0);
    }
}
