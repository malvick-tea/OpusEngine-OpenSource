using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Engine.Rhi.Direct3D12;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Per-material <see cref="IMaterialAtlas"/> implementation. Owns one SRV heap holding
/// <c>(MaterialCount + 1)</c> contiguous five-descriptor runs (see <see cref="PbrMaterialMaps"/>):
/// <list type="bullet">
/// <item><description>Runs <c>0..MaterialCount-1</c> — one per glTF material; each run's five SRVs
/// view that material's base/normal/metallic-roughness/occlusion/emissive textures, substituting a
/// neutral fallback (white or flat normal) for any map the material omits.</description></item>
/// <item><description>Run <c>MaterialCount</c> — the all-neutral fallback, bound when a primitive's
/// material index is null or out of range.</description></item>
/// </list>
/// Underlying textures are deduplicated: an image referenced by several materials (or several map
/// kinds) is uploaded once and viewed by several descriptors. <see cref="Dispose"/> releases the
/// heap, the unique image textures, and the two fallback texels exactly once. Built via
/// <see cref="MultiMaterialAtlasBuilder.BuildFromGlb"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "MA0055:Do not use finalizer",
    Justification = "The finalizer releases only the unmanaged descriptor heap.")]
public sealed unsafe class MultiMaterialAtlas : IMaterialAtlas
{
    private static readonly ResolvedMaterial NeutralFactors = new(
        default, Vector4.One, MetallicFactor: 0f, RoughnessFactor: 1f, Vector3.Zero);

    private readonly D3D12Texture[] _uniqueImageTextures;
    private readonly D3D12Texture _white;
    private readonly D3D12Texture _flatNormal;
    private ID3D12DescriptorHeap* _heap;
    private readonly ResolvedMaterial[] _materials;
    private readonly ResolvedMaterial _fallback;
    private bool _disposed;

    ~MultiMaterialAtlas()
    {
        ReleaseHeap();
    }

    internal MultiMaterialAtlas(
        D3D12RhiDevice device,
        D3D12Texture white,
        D3D12Texture flatNormal,
        IReadOnlyList<D3D12Texture> uniqueImageTextures,
        IReadOnlyList<MultiMaterialSlot> materialSlots)
    {
        _white = white;
        _flatNormal = flatNormal;
        _uniqueImageTextures = new D3D12Texture[uniqueImageTextures.Count];
        for (var i = 0; i < uniqueImageTextures.Count; i++)
        {
            _uniqueImageTextures[i] = uniqueImageTextures[i];
        }

        ID3D12DescriptorHeap* heap = null;
        try
        {
            var runCount = checked((uint)materialSlots.Count + 1u);
            heap = device.CreateSrvDescriptorHeap(
                checked(runCount * PbrMaterialMaps.MapsPerMaterial));

            _materials = new ResolvedMaterial[materialSlots.Count];
            for (var m = 0; m < materialSlots.Count; m++)
            {
                var slot = materialSlots[m];
                var table = PbrMaterialMaps.WriteRun(
                    device, heap, (uint)m,
                    ImageOr(slot.UniqueImageSlot, white),
                    ImageOr(slot.NormalSlot, flatNormal),
                    ImageOr(slot.MetallicRoughnessSlot, white),
                    ImageOr(slot.OcclusionSlot, white),
                    ImageOr(slot.EmissiveSlot, white));
                _materials[m] = new ResolvedMaterial(
                    table,
                    slot.Factor,
                    slot.MetallicFactor,
                    slot.RoughnessFactor,
                    slot.EmissiveFactor);
            }

            var fallbackTable = PbrMaterialMaps.WriteRun(
                device,
                heap,
                (uint)materialSlots.Count,
                white,
                flatNormal,
                white,
                white,
                white);
            _fallback = NeutralFactors with { MapTable = fallbackTable };
            _heap = heap;
            heap = null;
        }
        catch
        {
            if (heap != null)
            {
                heap->Release();
            }

            DisposeTextures();
            throw;
        }
    }

    /// <summary>How many distinct embedded images the atlas uploaded. Exposed for test
    /// assertions and diagnostic logging.</summary>
    public int UniqueImageCount => _uniqueImageTextures.Length;

    /// <summary>How many glTF materials the atlas resolves through <see cref="Resolve"/>.
    /// Exposed for test assertions; a primitive with <c>MaterialIndex &gt;= MaterialCount</c>
    /// falls back to the neutral run.</summary>
    public int MaterialCount => _materials.Length;

    public void BindHeapTo(D3D12CommandList commandList) => commandList.SetDescriptorHeaps(_heap);

    public ResolvedMaterial Resolve(int? materialIndex)
        => materialIndex is int idx && idx >= 0 && idx < _materials.Length ? _materials[idx] : _fallback;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
        ReleaseHeap();
        DisposeTextures();
    }

    private D3D12Texture ImageOr(int? slot, D3D12Texture fallback)
        => slot is int s && s >= 0 && s < _uniqueImageTextures.Length ? _uniqueImageTextures[s] : fallback;

    private void DisposeTextures()
    {
        foreach (var texture in _uniqueImageTextures)
        {
            texture.Dispose();
        }

        _white.Dispose();
        _flatNormal.Dispose();
    }

    private void ReleaseHeap()
    {
        if (_heap != null)
        {
            _heap->Release();
            _heap = null;
        }
    }
}
