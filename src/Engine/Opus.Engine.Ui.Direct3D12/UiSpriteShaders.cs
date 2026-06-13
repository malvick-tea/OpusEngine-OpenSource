namespace Opus.Engine.Ui.Direct3D12;

/// <summary>Production HLSL for the 2D UI sprite pipeline behind <see cref="D3D12DrawSurface"/>.
/// One PSO drives every <c>IDrawSurface</c> primitive — solid rects, lines, glyph runs sample
/// the R8 coverage atlas; filled circles and rings are shaded analytically so curvature
/// stays crisp at any radius.
/// <para>
/// Bindings match <c>D3D12RootSignatureFactory.CreateUiSprite</c>: 2-DWORD root
/// constants(b0, vertex — viewport size in pixels) + SRV table(t0, pixel — glyph + white-texel
/// atlas) + linear-clamp sampler(s0). Vertex layout matches <c>CreateUiSprite</c>: position +
/// uv + rgba8 + mode + shape params, 32-byte stride.
/// </para></summary>
internal static class UiSpriteShaders
{
    public const string VertexShader = """
        cbuffer Viewport : register(b0)
        {
            float ViewportWidth;
            float ViewportHeight;
        };

        struct VsIn
        {
            float2 pos   : POSITION0;
            float2 uv    : TEXCOORD0;
            float4 col   : COLOR0;
            float  mode  : TEXCOORD1;
            float2 shape : TEXCOORD2;
        };

        struct VsOut
        {
            float4 pos   : SV_POSITION;
            float2 uv    : TEXCOORD0;
            float4 col   : COLOR0;
            float  mode  : TEXCOORD1;
            float2 shape : TEXCOORD2;
        };

        VsOut main(VsIn input)
        {
            VsOut output;
            float x = (input.pos.x / ViewportWidth)  * 2.0 - 1.0;
            float y = 1.0 - (input.pos.y / ViewportHeight) * 2.0;
            output.pos   = float4(x, y, 0.0, 1.0);
            output.uv    = input.uv;
            output.col   = input.col;
            output.mode  = input.mode;
            output.shape = input.shape;
            return output;
        }
        """;

    public const string PixelShader = """
        Texture2D    g_atlas   : register(t0);
        SamplerState g_sampler : register(s0);

        struct PsIn
        {
            float4 pos   : SV_POSITION;
            float2 uv    : TEXCOORD0;
            float4 col   : COLOR0;
            float  mode  : TEXCOORD1;
            float2 shape : TEXCOORD2;
        };

        static const int MODE_TEXTURED      = 0;
        static const int MODE_CIRCLE        = 1;
        static const int MODE_RING          = 2;
        static const int MODE_TEXTURED_RGBA = 3;

        float CircleCoverage(float2 uv, float feather)
        {
            float d = distance(uv, float2(0.5, 0.5));
            return saturate((0.5 - d) / max(feather, 1e-4));
        }

        float RingCoverage(float2 uv, float feather, float innerFraction)
        {
            float d = distance(uv, float2(0.5, 0.5));
            float f = max(feather, 1e-4);
            float outer = saturate((0.5 - d) / f);
            float inner = saturate((d - 0.5 * innerFraction) / f);
            return outer * inner;
        }

        float4 main(PsIn input) : SV_TARGET
        {
            int mode = int(round(input.mode));
            if (mode == MODE_TEXTURED_RGBA)
            {
                return g_atlas.Sample(g_sampler, input.uv) * input.col;
            }

            float coverage = 1.0;
            if (mode == MODE_TEXTURED)
            {
                coverage = g_atlas.Sample(g_sampler, input.uv).r;
            }
            else if (mode == MODE_CIRCLE)
            {
                coverage = CircleCoverage(input.uv, input.shape.x);
            }
            else
            {
                coverage = RingCoverage(input.uv, input.shape.x, input.shape.y);
            }

            float4 result = input.col;
            result.a *= coverage;
            return result;
        }
        """;
}
