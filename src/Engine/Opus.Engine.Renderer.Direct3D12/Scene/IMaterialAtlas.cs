using System;
using System.Numerics;
using Opus.Engine.Rhi.Direct3D12;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>The per-material binding <see cref="IMaterialAtlas.Resolve"/> hands the
/// <see cref="ForwardScenePass"/> for one primitive. <see cref="MapTable"/> is the GPU handle of
/// the material's contiguous five-SRV run (base colour <c>t0</c>, normal <c>t1</c>,
/// metallic-roughness <c>t2</c>, occlusion <c>t3</c>, emissive <c>t4</c>); the scalar factors
/// multiply the sampled maps so an untextured material still shades from its glTF factors.</summary>
public readonly record struct ResolvedMaterial(
    GpuDescriptorHandle MapTable,
    Vector4 BaseColorFactor,
    float MetallicFactor,
    float RoughnessFactor,
    Vector3 EmissiveFactor);

/// <summary>Pluggable per-primitive material resolver consumed by
/// <see cref="ForwardScenePass"/>. Implementations decide how a glTF
/// <c>material → SRV run</c> mapping happens — single-texture, full metal-roughness
/// catalogue, deferred decal, etc. Every implementation binds the same five-map run shape so
/// the one forward PBR pixel shader can sample base/normal/metallic-roughness/occlusion/emissive
/// regardless of which atlas fed it.
/// <para>
/// The pass calls <see cref="BindHeapTo"/> once per frame before issuing draws, then
/// <see cref="Resolve"/> per primitive. The returned <see cref="ResolvedMaterial.MapTable"/> is
/// the base of the material's five-SRV run; the factors are the per-material PBR multipliers.
/// </para>
/// </summary>
public unsafe interface IMaterialAtlas : IDisposable
{
    /// <summary>Binds the descriptor heap that backs this atlas's SRV tables onto
    /// <paramref name="commandList"/>. Idempotent within a frame; call once before the
    /// first draw of a pass that will consume this atlas.</summary>
    void BindHeapTo(D3D12CommandList commandList);

    /// <summary>Resolves a primitive's material index to its five-SRV map run + scalar
    /// factors. Implementations fall back to a neutral run (white base/metallic-roughness/
    /// occlusion, flat normal) when the material index is null or out of range; the contract
    /// is that the returned run is always valid for sampling.</summary>
    ResolvedMaterial Resolve(int? materialIndex);
}
