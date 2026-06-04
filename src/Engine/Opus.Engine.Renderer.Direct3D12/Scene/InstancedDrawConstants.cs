using System.Numerics;
using System.Runtime.InteropServices;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Per-draw 32-bit root constants bound at <c>b1</c> for each primitive draw inside
/// <see cref="ForwardScenePass"/>. 13 DWORDs resolved from the <see cref="IMaterialAtlas"/>: the
/// per-material base-colour factor (4), the metallic + roughness scalars packed in
/// <see cref="MetalRoughness"/> (4), the emissive colour factor (4), and the instance-buffer
/// offset (1) of the mesh batch this draw fans across. The vertex shader reads its instance at
/// <c>g_instances[InstanceOffset + SV_InstanceID]</c>; the pixel shader multiplies each sampled
/// PBR map by its matching factor.
/// <para>
/// Field order + the float4 padding match the HLSL <c>cbuffer DrawConstants</c> in
/// <see cref="ForwardSceneShaders"/> so the raw DWORD copy lands each value at the register the
/// shader reads. Pairs with the root signature
/// <c>D3D12RootSignatureFactory.CreateInstancedPbrScene(num32BitValues: Num32BitValues)</c>.
/// </para></summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct InstancedDrawConstants
{
    /// <summary>Number of 32-bit values this struct occupies (three float4 + uint offset). Bind via
    /// <c>SetGraphicsRoot32BitConstants(_, numValues: InstancedDrawConstants.Num32BitValues, _)</c>.</summary>
    public const uint Num32BitValues = 13u;

    /// <summary>RGBA base-colour multiplier (the shader uses <c>.rgb</c>).</summary>
    public Vector4 MaterialFactor;

    /// <summary>Metalness in <c>X</c>, roughness in <c>Y</c>; <c>Z/W</c> are float4 padding.</summary>
    public Vector4 MetalRoughness;

    /// <summary>Linear emissive colour in <c>XYZ</c>; <c>W</c> is float4 padding.</summary>
    public Vector4 EmissiveFactor;

    public uint InstanceOffset;
}
