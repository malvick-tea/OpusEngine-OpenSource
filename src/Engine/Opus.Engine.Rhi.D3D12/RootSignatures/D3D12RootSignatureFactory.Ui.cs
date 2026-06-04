using Silk.NET.Direct3D12;
using static Opus.Engine.Rhi.Direct3D12.DescriptorRangeBuilder;
using static Opus.Engine.Rhi.Direct3D12.RootParameterBuilder;
using static Opus.Engine.Rhi.Direct3D12.StaticSamplerLibrary;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Root signature for the 2D UI sprite pipeline — the batched-quad path that
/// backs <c>IDrawSurface</c> on D3D12 (screens, HUD, commander map). Every UI primitive
/// is a textured quad, so one signature serves rects, lines, circles and glyph runs.</summary>
public static unsafe partial class D3D12RootSignatureFactory
{
    private const uint UiViewportConstantCount = 2u; // viewport width + height, in pixels

    /// <summary>UI sprite batch: 2-DWORD constants(b0, vertex — viewport size for the
    /// pixel→clip transform) + single SRV table(t0, pixel — the glyph / white-texel atlas)
    /// + linear-clamp sampler(s0, pixel). The input-assembler flag is set because the
    /// pipeline streams a packed quad vertex buffer.</summary>
    public static D3D12RootSignature CreateUiSprite(D3D12RhiDevice device)
    {
        var srvRange = stackalloc DescriptorRange[1];
        srvRange[0] = SrvRange(count: 1u);

        var p = stackalloc RootParameter[2];
        p[0] = RootConstants(shaderRegister: 0u, UiViewportConstantCount, ShaderVisibility.Vertex);
        p[1] = DescriptorTable(srvRange, 1u, ShaderVisibility.Pixel);

        var sampler = LinearClamp(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(
            device, p, 2u, &sampler, 1u, RootSignatureFlags.AllowInputAssemblerInputLayout);
    }
}
