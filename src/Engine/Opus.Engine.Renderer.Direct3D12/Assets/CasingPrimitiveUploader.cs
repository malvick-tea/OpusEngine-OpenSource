using System;
using System.Runtime.InteropServices;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;

namespace Opus.Engine.Renderer.Direct3D12.Assets;

/// <summary>Uploads the procedural <see cref="CasingMesh"/> cylinder to a fresh VB + IB
/// pair and returns a <see cref="GpuPrimitive"/>. Declared with <c>MaterialIndex = 0</c>
/// so the atlas resolves to its main factor — casings render with the tank albedo
/// modulator (same path as the projectile cube) until per-asset casing textures arrive.</summary>
public static class CasingPrimitiveUploader
{
    public static GpuPrimitive Upload(
        D3D12RhiDevice device,
        string namePrefix,
        float radiusMeters = CasingMesh.DefaultRadiusMeters,
        float halfLengthMeters = CasingMesh.DefaultHalfLengthMeters)
    {
        ArgumentNullException.ThrowIfNull(device);

        var verts = CasingMesh.BuildVertices(radiusMeters, halfLengthMeters);
        var indices = CasingMesh.BuildIndices();

        var vb = device.CreateGraphicsBuffer(new RhiBufferDescription(
            $"{namePrefix}.casing.verts",
            verts.Length * Marshal.SizeOf<GltfVertexPosNormalUv>(),
            RhiBufferUsage.Vertex));
        BufferUploadHelper.WriteStructs(vb, verts);

        var ib = device.CreateGraphicsBuffer(new RhiBufferDescription(
            $"{namePrefix}.casing.indices",
            indices.Length * sizeof(uint),
            RhiBufferUsage.Index));
        BufferUploadHelper.WriteStructs(ib, indices);

        return new GpuPrimitive(vb, ib, (uint)indices.Length, MaterialIndex: 0);
    }
}
