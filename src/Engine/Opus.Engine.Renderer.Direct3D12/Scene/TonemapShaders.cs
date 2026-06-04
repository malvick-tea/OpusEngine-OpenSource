namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Runtime HLSL for <see cref="TonemapPass"/>: a bufferless fullscreen
/// triangle that samples the scene's HDR target, applies Narkowicz ACES filmic curve,
/// gamma-encodes the result (2.2), and writes to the swap-chain backbuffer.
/// <para>
/// Bindings match <c>D3D12RootSignatureFactory.CreateTonemapPost</c>: 4-DWORD root
/// constants(b0, pixel) carry exposure (currently unused — reserved for HDR exposure
/// control follow-up); SRV table(t0, pixel) = HDR target; static linear-clamp sampler(s0).
/// </para></summary>
internal static class TonemapShaders
{
    public const string VertexShader = """
        struct VsOut {
            float4 pos : SV_POSITION;
            float2 uv  : TEXCOORD0;
        };
        VsOut main(uint vid : SV_VERTEXID)
        {
            VsOut o;
            float2 uv = float2((vid << 1) & 2, vid & 2);
            o.pos = float4(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0, 0.0, 1.0);
            o.uv  = uv;
            return o;
        }
        """;

    public const string PixelShader = """
        Texture2D    g_hdr     : register(t0);
        SamplerState g_sampler : register(s0);

        struct PsIn {
            float4 pos : SV_POSITION;
            float2 uv  : TEXCOORD0;
        };

        float3 ACESFilmic(float3 x)
        {
            const float a = 2.51;
            const float b = 0.03;
            const float c = 2.43;
            const float d = 0.59;
            const float e = 0.14;
            return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
        }

        float4 main(PsIn input) : SV_TARGET
        {
            float4 hdr = g_hdr.Sample(g_sampler, input.uv);
            float3 ldr = ACESFilmic(hdr.rgb);
            float3 gamma = pow(ldr, 1.0 / 2.2);
            // Preserve HDR alpha so the offscreen-target path can composite onto a UI
            // surface with sky-pixels (alpha=0) reading through to the chrome underneath.
            // The swap-chain path clears HDR alpha to 1.0, so the result still presents
            // opaque on the back buffer.
            return float4(gamma, hdr.a);
        }
        """;
}
