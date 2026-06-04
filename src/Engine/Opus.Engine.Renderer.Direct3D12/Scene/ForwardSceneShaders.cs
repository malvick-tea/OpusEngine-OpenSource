namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Runtime HLSL for the instanced <see cref="ForwardScenePass"/>. Forward metal-roughness
/// PBR pipeline: the VS reads its per-instance world matrix + tint from the instance
/// <c>StructuredBuffer</c> (root SRV <c>t5</c>) at <c>InstanceOffset + SV_InstanceID</c>, transforms
/// POS/NORMAL, and forwards world position, UV + the combined tint. The PS samples the five-map run
/// (base colour <c>t0</c>, normal <c>t1</c>, metallic-roughness <c>t2</c>, occlusion <c>t3</c>,
/// emissive <c>t4</c>) the <see cref="IMaterialAtlas"/> bound, derives a tangent frame from
/// screen-space derivatives (no tangent vertex attribute), and shades with a Cook-Torrance GGX BRDF
/// under one directional sun + flat ambient. Output target is HDR (R16G16B16A16_FLOAT) — tonemapping
/// happens in <see cref="TonemapPass"/>.
/// <para>
/// Bindings match <c>D3D12RootSignatureFactory.CreateInstancedPbrScene(num32BitValues:
/// InstancedDrawConstants.Num32BitValues)</c>: CBV(b0) = <see cref="ForwardSceneConstants"/>,
/// 13-DWORD root constants(b1) = <see cref="InstancedDrawConstants"/> (per-material base/metal-rough/
/// emissive factors + batch instance offset), SRV table(t0..t4) = the per-material map run, root
/// SRV(t5) = the per-frame instance buffer, static anisotropic-wrap sampler(s0).
/// </para></summary>
internal static class ForwardSceneShaders
{
    public const string SceneVertexShader = """
        cbuffer Scene : register(b0)
        {
            row_major float4x4 ViewProjection;
            float4 SunDirection;
            float4 SunColor;
            float4 AmbientColor;
            float4 CameraPosition;
        };
        cbuffer DrawConstants : register(b1)
        {
            float4 MaterialFactor;
            float4 MetalRoughness;
            float4 EmissiveFactor;
            uint   InstanceOffset;
        };
        struct InstanceData {
            row_major float4x4 World;
            float4 BaseColorFactor;
            float2 UvOffset;
        };
        StructuredBuffer<InstanceData> g_instances : register(t5);

        struct VsIn  { float3 pos : POSITION; float3 nrm : NORMAL; float2 uv : TEXCOORD0; };
        struct VsOut {
            float4 pos      : SV_POSITION;
            float3 worldPos : TEXCOORD1;
            float3 worldNrm : NORMAL;
            float2 uv       : TEXCOORD0;
            float3 baseTint : COLOR;
        };

        VsOut main(VsIn input, uint instanceId : SV_InstanceID)
        {
            InstanceData inst = g_instances[InstanceOffset + instanceId];
            VsOut o;
            float4 wp = mul(float4(input.pos, 1.0), inst.World);
            o.pos = mul(wp, ViewProjection);
            o.worldPos = wp.xyz;
            o.worldNrm = mul(float4(input.nrm, 0.0), inst.World).xyz;
            o.uv = input.uv + inst.UvOffset;
            o.baseTint = MaterialFactor.rgb * inst.BaseColorFactor.rgb;
            return o;
        }
        """;

    public const string ScenePixelShader = """
        cbuffer Scene : register(b0)
        {
            row_major float4x4 ViewProjection;
            float4 SunDirection;
            float4 SunColor;
            float4 AmbientColor;
            float4 CameraPosition;
        };
        cbuffer DrawConstants : register(b1)
        {
            float4 MaterialFactor;
            float4 MetalRoughness;   // x = metallic, y = roughness
            float4 EmissiveFactor;   // xyz emissive
            uint   InstanceOffset;
        };

        Texture2D    g_baseColor : register(t0);
        Texture2D    g_normal    : register(t1);
        Texture2D    g_mra       : register(t2);   // glTF metallic-roughness: G = roughness, B = metallic
        Texture2D    g_occlusion : register(t3);   // R = ambient occlusion
        Texture2D    g_emissive  : register(t4);
        SamplerState g_sampler   : register(s0);

        static const float PI = 3.14159265;

        struct PsIn {
            float4 pos      : SV_POSITION;
            float3 worldPos : TEXCOORD1;
            float3 worldNrm : NORMAL;
            float2 uv       : TEXCOORD0;
            float3 baseTint : COLOR;
        };

        // Christian Schueler's cotangent frame: a per-pixel TBN from screen-space derivatives of the
        // world position + UV, so normal mapping needs no precomputed tangent vertex attribute.
        float3 perturbNormal(float3 N, float3 worldPos, float2 uv)
        {
            float3 mapN = g_normal.Sample(g_sampler, uv).xyz * 2.0 - 1.0;
            float3 dp1 = ddx(worldPos);
            float3 dp2 = ddy(worldPos);
            float2 duv1 = ddx(uv);
            float2 duv2 = ddy(uv);
            float3 dp2perp = cross(dp2, N);
            float3 dp1perp = cross(N, dp1);
            float3 T = dp2perp * duv1.x + dp1perp * duv2.x;
            float3 B = dp2perp * duv1.y + dp1perp * duv2.y;
            float invmax = rsqrt(max(dot(T, T), dot(B, B)));
            float3x3 tbn = float3x3(T * invmax, B * invmax, N);
            return normalize(mul(mapN, tbn));
        }

        float distributionGgx(float nDotH, float roughness)
        {
            float a = roughness * roughness;
            float a2 = a * a;
            float d = nDotH * nDotH * (a2 - 1.0) + 1.0;
            return a2 / max(PI * d * d, 1e-4);
        }

        float geometrySmith(float nDotV, float nDotL, float roughness)
        {
            float a = roughness * roughness;
            float k = a * 0.5;
            float gv = nDotV / (nDotV * (1.0 - k) + k);
            float gl = nDotL / (nDotL * (1.0 - k) + k);
            return gv * gl;
        }

        float3 fresnelSchlick(float vDotH, float3 f0)
        {
            return f0 + (1.0 - f0) * pow(1.0 - vDotH, 5.0);
        }

        float4 main(PsIn input) : SV_TARGET
        {
            float3 baseColor = g_baseColor.Sample(g_sampler, input.uv).rgb * input.baseTint;
            float2 mr = g_mra.Sample(g_sampler, input.uv).gb;
            float roughness = clamp(mr.x * MetalRoughness.y, 0.04, 1.0);
            float metallic = saturate(mr.y * MetalRoughness.x);
            float ao = g_occlusion.Sample(g_sampler, input.uv).r;
            float3 emissive = g_emissive.Sample(g_sampler, input.uv).rgb * EmissiveFactor.rgb;

            float3 N = perturbNormal(normalize(input.worldNrm), input.worldPos, input.uv);
            float3 V = normalize(CameraPosition.xyz - input.worldPos);
            float3 L = normalize(SunDirection.xyz);
            float3 H = normalize(V + L);

            float nDotL = saturate(dot(N, L));
            float nDotV = saturate(dot(N, V)) + 1e-4;
            float nDotH = saturate(dot(N, H));
            float vDotH = saturate(dot(V, H));

            float3 f0 = lerp(float3(0.04, 0.04, 0.04), baseColor, metallic);
            float3 diffuseColor = baseColor * (1.0 - metallic);

            float d = distributionGgx(nDotH, roughness);
            float g = geometrySmith(nDotV, nDotL, roughness);
            float3 f = fresnelSchlick(vDotH, f0);

            // The * PI cancels the 1/PI in distributionGgx so the specular sits at the same
            // un-normalised magnitude as the Lambert diffuse below — assets that bind no PBR maps
            // keep looking like the legacy flat-Lambert pass instead of darkening by a factor of PI.
            float3 specular = (d * g * f) * PI / max(4.0 * nDotV * nDotL, 1e-4);
            float3 kd = (1.0 - f) * (1.0 - metallic);

            float3 direct = (kd * diffuseColor + specular) * SunColor.rgb * nDotL;
            float3 ambient = diffuseColor * AmbientColor.rgb * ao;
            return float4(direct + ambient + emissive, 1.0);
        }
        """;
}
