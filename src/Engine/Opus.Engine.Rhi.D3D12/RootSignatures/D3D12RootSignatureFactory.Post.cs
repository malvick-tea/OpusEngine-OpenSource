using Silk.NET.Direct3D12;
using static Opus.Engine.Rhi.Direct3D12.DescriptorRangeBuilder;
using static Opus.Engine.Rhi.Direct3D12.RootParameterBuilder;
using static Opus.Engine.Rhi.Direct3D12.StaticSamplerLibrary;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Post-process / screen-space root signatures: SSAO, tonemap, bloom composite, skybox.
/// Bufferless fullscreen draws — no IA layout required.</summary>
public static unsafe partial class D3D12RootSignatureFactory
{
    private const uint PostRootConstantCount = 4u; // exposure + 3 reserved knobs
    private const uint TonemapCompositeSrvCount = 2u; // HDR + bloom mip0
    private const uint TaaResolveSrvCount = 3u; // current HDR + history + motion vectors

    /// <summary>SSAO: CBV(b0, pixel) + depth SRV table(t0, pixel) + linear-clamp(s0, pixel).</summary>
    public static D3D12RootSignature CreateSsao(D3D12RhiDevice device)
    {
        var srvRange = stackalloc DescriptorRange[1];
        srvRange[0] = SrvRange(count: 1u);

        var p = stackalloc RootParameter[2];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.Pixel);
        p[1] = DescriptorTable(srvRange, 1u, ShaderVisibility.Pixel);

        var sampler = LinearClamp(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 2u, &sampler, 1u, RootSignatureFlags.None);
    }

    /// <summary>Tonemap composite (HDR + bloom): 4-DWORD constants(b0, pixel) + 2-SRV table(t0..t1, pixel) + linear-clamp(s0, pixel).</summary>
    public static D3D12RootSignature CreateTonemapComposite(D3D12RhiDevice device)
    {
        var srvRange = stackalloc DescriptorRange[1];
        srvRange[0] = SrvRange(count: TonemapCompositeSrvCount);

        var p = stackalloc RootParameter[2];
        p[0] = RootConstants(shaderRegister: 0u, PostRootConstantCount, ShaderVisibility.Pixel);
        p[1] = DescriptorTable(srvRange, 1u, ShaderVisibility.Pixel);

        var sampler = LinearClamp(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 2u, &sampler, 1u, RootSignatureFlags.None);
    }

    /// <summary>Simple post tonemap: 4-DWORD constants(b0, pixel) + single SRV table(t0, pixel) + linear-clamp(s0, pixel).</summary>
    public static D3D12RootSignature CreateTonemapPost(D3D12RhiDevice device)
    {
        var srvRange = stackalloc DescriptorRange[1];
        srvRange[0] = SrvRange(count: 1u);

        var p = stackalloc RootParameter[2];
        p[0] = RootConstants(shaderRegister: 0u, PostRootConstantCount, ShaderVisibility.Pixel);
        p[1] = DescriptorTable(srvRange, 1u, ShaderVisibility.Pixel);

        var sampler = LinearClamp(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 2u, &sampler, 1u, RootSignatureFlags.None);
    }

    /// <summary>Skybox: CBV(b0, all) + cubemap SRV table(t0, pixel) + linear-clamp(s0, pixel). Bufferless triangle, no IA.</summary>
    public static D3D12RootSignature CreateSkybox(D3D12RhiDevice device)
    {
        var srvRange = stackalloc DescriptorRange[1];
        srvRange[0] = SrvRange(count: 1u);

        var p = stackalloc RootParameter[2];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.All);
        p[1] = DescriptorTable(srvRange, 1u, ShaderVisibility.Pixel);

        var sampler = LinearClamp(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 2u, &sampler, 1u, RootSignatureFlags.None);
    }

    /// <summary>R-14.b motion-vector reconstruct: CBV(b0, pixel — current+prev VP) + depth SRV table(t0, pixel) + linear-clamp(s0, pixel).
    /// Output target = R16G16_FLOAT, holds per-pixel screen-space velocity in UV-delta units.</summary>
    public static D3D12RootSignature CreateMotionVectorReconstruct(D3D12RhiDevice device)
    {
        var srvRange = stackalloc DescriptorRange[1];
        srvRange[0] = SrvRange(count: 1u);

        var p = stackalloc RootParameter[2];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.Pixel);
        p[1] = DescriptorTable(srvRange, 1u, ShaderVisibility.Pixel);

        var sampler = LinearClamp(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 2u, &sampler, 1u, RootSignatureFlags.None);
    }

    /// <summary>R-14.b TAA resolve: 4-DWORD constants(b0, pixel — feedback + screen size) + 3-SRV table(t0..t2 = current/history/motion, pixel) + linear-clamp(s0, pixel).</summary>
    public static D3D12RootSignature CreateTaaResolve(D3D12RhiDevice device)
    {
        var srvRange = stackalloc DescriptorRange[1];
        srvRange[0] = SrvRange(count: TaaResolveSrvCount);

        var p = stackalloc RootParameter[2];
        p[0] = RootConstants(shaderRegister: 0u, PostRootConstantCount, ShaderVisibility.Pixel);
        p[1] = DescriptorTable(srvRange, 1u, ShaderVisibility.Pixel);

        var sampler = LinearClamp(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 2u, &sampler, 1u, RootSignatureFlags.None);
    }

    /// <summary>R-14.c screen-space reflections: CBV(b0, pixel — projection + invProjection + ray params)
    /// + 2-SRV table(t0..t1 = depth + HDR, pixel) + linear-clamp(s0, pixel). Output target = RGBA16F
    /// where rgb = reflection colour × reflectivity, a = hit confidence.</summary>
    public static D3D12RootSignature CreateScreenSpaceReflections(D3D12RhiDevice device)
    {
        var srvRange = stackalloc DescriptorRange[1];
        srvRange[0] = SrvRange(count: 2u);

        var p = stackalloc RootParameter[2];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.Pixel);
        p[1] = DescriptorTable(srvRange, 1u, ShaderVisibility.Pixel);

        var sampler = LinearClamp(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 2u, &sampler, 1u, RootSignatureFlags.None);
    }

    /// <summary>R-14.d volumetric fog ray-march: CBV(b0, pixel — invVP + camera + sun + fog params)
    /// + depth SRV table(t0, pixel) + linear-clamp(s0, pixel). Output target = RGBA16F where
    /// rgb = in-scattered light, a = transmittance through the volume.</summary>
    public static D3D12RootSignature CreateVolumetricFog(D3D12RhiDevice device)
    {
        var srvRange = stackalloc DescriptorRange[1];
        srvRange[0] = SrvRange(count: 1u);

        var p = stackalloc RootParameter[2];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.Pixel);
        p[1] = DescriptorTable(srvRange, 1u, ShaderVisibility.Pixel);

        var sampler = LinearClamp(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 2u, &sampler, 1u, RootSignatureFlags.None);
    }

    /// <summary>R-15.a deferred decal: CBV(b0, pixel — per-decal invDecalWorld + invVP + colour + pattern)
    /// + depth SRV table(t0, pixel) + linear-clamp(s0, pixel). Output is the existing HDR target with
    /// alpha-blend enabled in the PSO. Pass draws a fullscreen triangle per decal; the PS rejects
    /// pixels outside the unit cube in decal-local space.</summary>
    public static D3D12RootSignature CreateDeferredDecal(D3D12RhiDevice device)
    {
        var srvRange = stackalloc DescriptorRange[1];
        srvRange[0] = SrvRange(count: 1u);

        var p = stackalloc RootParameter[2];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.Pixel);
        p[1] = DescriptorTable(srvRange, 1u, ShaderVisibility.Pixel);

        var sampler = LinearClamp(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 2u, &sampler, 1u, RootSignatureFlags.None);
    }

    /// <summary>R-15.b bokeh depth-of-field: CBV(b0, pixel — invProjection + focus params + inv screen)
    /// + 2-SRV table(t0..t1 = HDR + depth, pixel) + linear-clamp(s0, pixel). Output target = RGBA16F,
    /// disc-blurred HDR weighted by per-pixel circle of confusion.</summary>
    public static D3D12RootSignature CreateBokehDepthOfField(D3D12RhiDevice device)
    {
        var srvRange = stackalloc DescriptorRange[1];
        srvRange[0] = SrvRange(count: 2u);

        var p = stackalloc RootParameter[2];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.Pixel);
        p[1] = DescriptorTable(srvRange, 1u, ShaderVisibility.Pixel);

        var sampler = LinearClamp(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 2u, &sampler, 1u, RootSignatureFlags.None);
    }

    /// <summary>R-15.c motion blur: 4-DWORD constants(b0, pixel — sample count, max blur length, screen inv)
    /// + 2-SRV table(t0..t1 = HDR + motion vectors, pixel) + linear-clamp(s0, pixel). Output target = RGBA16F.</summary>
    public static D3D12RootSignature CreateMotionBlur(D3D12RhiDevice device)
    {
        var srvRange = stackalloc DescriptorRange[1];
        srvRange[0] = SrvRange(count: 2u);

        var p = stackalloc RootParameter[2];
        p[0] = RootConstants(shaderRegister: 0u, PostRootConstantCount, ShaderVisibility.Pixel);
        p[1] = DescriptorTable(srvRange, 1u, ShaderVisibility.Pixel);

        var sampler = LinearClamp(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 2u, &sampler, 1u, RootSignatureFlags.None);
    }

    /// <summary>R-15.d chromatic aberration + vignette: 4-DWORD constants(b0, pixel — CA offset, vignette params)
    /// + single SRV table(t0, pixel) + linear-clamp(s0, pixel). Output = swapchain format (LDR final).</summary>
    public static D3D12RootSignature CreateChromaticVignette(D3D12RhiDevice device)
    {
        var srvRange = stackalloc DescriptorRange[1];
        srvRange[0] = SrvRange(count: 1u);

        var p = stackalloc RootParameter[2];
        p[0] = RootConstants(shaderRegister: 0u, PostRootConstantCount, ShaderVisibility.Pixel);
        p[1] = DescriptorTable(srvRange, 1u, ShaderVisibility.Pixel);

        var sampler = LinearClamp(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 2u, &sampler, 1u, RootSignatureFlags.None);
    }

    /// <summary>R-16.a procedural sky / analytical atmospheric scattering. CBV(b0, pixel — sun dir,
    /// turbidity, rayleigh/mie scattering tints, sun radius, exposure) + linear-clamp(s0, pixel).
    /// Output target = HDR (RGBA16F). Bufferless fullscreen triangle reconstructs view ray from UV.</summary>
    public static D3D12RootSignature CreateAtmosphericSky(D3D12RhiDevice device)
    {
        var p = stackalloc RootParameter[1];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.Pixel);

        var sampler = LinearClamp(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 1u, &sampler, 1u, RootSignatureFlags.None);
    }

    /// <summary>R-16.b water surface. CBV(b0, all — view/proj + camera + wave/foam params + time
    /// + sky/water tints) + depth SRV(t0, pixel — for depth-based foam) + linear-clamp(s0, pixel).
    /// Reflection colour comes from CB to avoid an env-cubemap dependency for the test.</summary>
    public static D3D12RootSignature CreateWaterSurface(D3D12RhiDevice device)
    {
        var srvRange = stackalloc DescriptorRange[1];
        srvRange[0] = SrvRange(count: 1u);

        var p = stackalloc RootParameter[2];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.All);
        p[1] = DescriptorTable(srvRange, 1u, ShaderVisibility.Pixel);

        var sampler = LinearClamp(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 2u, &sampler, 1u, RootSignatureFlags.AllowInputAssemblerInputLayout);
    }

    /// <summary>R-16.c cel/toon shading scene. CBV(b0, all — scene lighting/cel bands) +
    /// 32-bit constants(b1, all — per-object world matrix + albedo + cel knobs). Mesh has
    /// pos+normal+color IA layout. HDR target output.</summary>
    public static D3D12RootSignature CreateCelToonScene(D3D12RhiDevice device)
    {
        var p = stackalloc RootParameter[2];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.All);
        p[1] = RootConstants(shaderRegister: 1u, num32BitValues: 24u, ShaderVisibility.All);

        return RootSignatureSerializer.Build(device, p, 2u, null, 0u, RootSignatureFlags.AllowInputAssemblerInputLayout);
    }

    /// <summary>R-16.d outline post-pass: 4-DWORD constants(b0, pixel — edge thresholds, screen inv)
    /// + 2-SRV table(t0 = scene HDR, t1 = depth, pixel) + linear-clamp(s0, pixel). Output =
    /// swapchain format (LDR with black edges composited). Normal is reconstructed from depth
    /// gradient in the PS, so no separate normal target is needed.</summary>
    public static D3D12RootSignature CreateOutlinePost(D3D12RhiDevice device)
    {
        var srvRange = stackalloc DescriptorRange[1];
        srvRange[0] = SrvRange(count: 2u);

        var p = stackalloc RootParameter[2];
        p[0] = RootConstants(shaderRegister: 0u, PostRootConstantCount, ShaderVisibility.Pixel);
        p[1] = DescriptorTable(srvRange, 1u, ShaderVisibility.Pixel);

        var sampler = LinearClamp(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 2u, &sampler, 1u, RootSignatureFlags.None);
    }

    /// <summary>R-18.b lens flare: CBV(b0, pixel — sun UV + aspect + flare intensity + occlusion radius
    /// + per-element packed parameters) + depth SRV table(t0, pixel) + linear-clamp(s0, pixel). Bufferless
    /// fullscreen triangle; PS analytically composes N flare elements along the sun→centre axis and modulates
    /// by an occlusion factor sampled from the depth target around the sun's screen position. Output is
    /// additively blended over an existing HDR target via the additive fullscreen PSO.</summary>
    public static D3D12RootSignature CreateLensFlare(D3D12RhiDevice device)
    {
        var srvRange = stackalloc DescriptorRange[1];
        srvRange[0] = SrvRange(count: 1u);

        var p = stackalloc RootParameter[2];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.Pixel);
        p[1] = DescriptorTable(srvRange, 1u, ShaderVisibility.Pixel);

        var sampler = LinearClamp(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 2u, &sampler, 1u, RootSignatureFlags.None);
    }
}
