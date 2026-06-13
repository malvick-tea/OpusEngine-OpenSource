using System;
using System.Runtime.InteropServices;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;

namespace Opus.Engine.Renderer.Direct3D12.Assets;

/// <summary>Uploads a unit cube — the same geometry as <see cref="ProjectileMesh"/> — as a
/// <c>MaterialIndex = null</c> primitive. With no material binding the atlas resolves it to its
/// white default slot (exactly as the floor does), so a per-instance tint reads as the box's final
/// colour instead of modulating a tank-camo texture. The match renderer fans this one cube across
/// every destructible street prop — a pole, a sign, a bin, a felled trunk — each scaled and tinted
/// from its catalogue size, so the city's clutter needs no per-prop mesh.</summary>
public static class PropBoxPrimitiveUploader
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
            $"{namePrefix}.propbox.verts",
            verts.Length * Marshal.SizeOf<GltfVertexPosNormalUv>(),
            RhiBufferUsage.Vertex));
        BufferUploadHelper.WriteStructs(vb, verts);

        var ib = device.CreateGraphicsBuffer(new RhiBufferDescription(
            $"{namePrefix}.propbox.indices",
            indices.Length * sizeof(uint),
            RhiBufferUsage.Index));
        BufferUploadHelper.WriteStructs(ib, indices);

        return new GpuPrimitive(vb, ib, (uint)indices.Length, MaterialIndex: null);
    }
}
