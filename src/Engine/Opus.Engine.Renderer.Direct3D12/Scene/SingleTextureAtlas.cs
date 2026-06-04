using System.Collections.Generic;
using System.Numerics;
using Opus.Engine.Rhi.Direct3D12;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Simplest <see cref="IMaterialAtlas"/> implementation: binds one albedo texture for
/// every primitive regardless of material index, with neutral PBR maps (flat normal, unit
/// metallic-roughness/occlusion, no emission) filling the rest of the shader's five-SRV run. Used
/// by smoke tests + procedurally-textured fixtures (floor / sky quads) where a single base-colour
/// texture is enough and the material shades as a matte dielectric. Material-chain-aware atlases
/// (per-material maps, KHR-spec-gloss resolution) live in <see cref="MultiMaterialAtlas"/>.
/// <para>
/// The atlas owns the SRV heap, the caller-supplied albedo <see cref="D3D12Texture"/>, and the two
/// neutral fallback texels it uploads in its constructor. On <see cref="Dispose"/>, all are released.
/// </para>
/// </summary>
public sealed unsafe class SingleTextureAtlas : IMaterialAtlas
{
    private readonly D3D12Texture _albedo;
    private readonly D3D12Texture _white;
    private readonly D3D12Texture _flatNormal;
    private readonly ID3D12DescriptorHeap* _srvHeap;
    private readonly ResolvedMaterial _textured;
    private readonly ResolvedMaterial _nullMaterial;
    private bool _disposed;

    public SingleTextureAtlas(D3D12RhiDevice device, D3D12Texture albedo, Vector4 baseColorFactor)
        : this(device, albedo, baseColorFactor, baseColorFactor)
    {
    }

    /// <summary>Variant that distinguishes the fallback factor used when a primitive's
    /// material index is null (procedurally-generated geometry, untextured fixtures). The
    /// SRV run stays the same — only the base-colour multiplier swaps. Useful for tinting a
    /// procedural floor or sky quad without bringing in a second material atlas.</summary>
    public SingleTextureAtlas(
        D3D12RhiDevice device,
        D3D12Texture albedo,
        Vector4 baseColorFactor,
        Vector4 nullMaterialFactor)
    {
        _albedo = albedo;

        using var initCmd = device.CreateGraphicsCommandList("single-atlas.init");
        var staging = new List<D3D12Buffer>(2);
        try
        {
            initCmd.Begin(0);
            _white = PbrMaterialMaps.CreateWhite(device, initCmd, "single-atlas.white", staging);
            _flatNormal = PbrMaterialMaps.CreateFlatNormal(device, initCmd, "single-atlas.flatnormal", staging);
            initCmd.End();
            initCmd.ExecuteOn(device);
            device.WaitForIdle();
        }
        finally
        {
            foreach (var buffer in staging)
            {
                buffer.Dispose();
            }
        }

        _srvHeap = device.CreateSrvDescriptorHeap(PbrMaterialMaps.MapsPerMaterial);
        var table = PbrMaterialMaps.WriteRun(
            device, _srvHeap, runIndex: 0u, _albedo, _flatNormal, _white, _white, _white);

        // Matte dielectric: no metalness, fully rough, no emission. Only the base-colour factor
        // distinguishes a textured primitive from a null-material (procedural) one.
        _textured = new ResolvedMaterial(table, baseColorFactor, MetallicFactor: 0f, RoughnessFactor: 1f, Vector3.Zero);
        _nullMaterial = _textured with { BaseColorFactor = nullMaterialFactor };
    }

    public void BindHeapTo(D3D12CommandList commandList) => commandList.SetDescriptorHeaps(_srvHeap);

    public ResolvedMaterial Resolve(int? materialIndex)
        => materialIndex.HasValue ? _textured : _nullMaterial;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _srvHeap->Release();
        _albedo.Dispose();
        _white.Dispose();
        _flatNormal.Dispose();
        _disposed = true;
    }
}
