using System.Numerics;
using System.Runtime.InteropServices;
using Opus.Engine.Renderer;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Scene-level constant buffer for <see cref="D3D12ForwardSceneRenderer"/>: the
/// view-projection matrix, the world-space camera position (the PBR view vector for GGX
/// specular), the directional sun, and a flat ambient term. Matches the HLSL <c>cbuffer Scene</c>
/// in <see cref="ForwardSceneShaders"/>. The PS uses the directional + ambient terms —
/// <see cref="LightingSetup.LocalLights"/> is ignored at this milestone (lands with M3-wrap.b's
/// light-culling sub-pass).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ForwardSceneConstants
{
    public Matrix4x4 ViewProjection;
    public Vector4 SunDirection;
    public Vector4 SunColor;
    public Vector4 AmbientColor;
    public Vector4 CameraPosition;

    /// <summary>Builds the constants from an abstract camera + lighting capture. Sun
    /// colour folds in <see cref="DirectionalLight.Intensity"/>; ambient uses the sky's
    /// zenith colour (horizon term ignored at v0). Padding alpha components are zero.</summary>
    public static ForwardSceneConstants From(
        in CameraSetup camera,
        in DirectionalLight sun,
        Vector3 ambientFallback)
    {
        var viewProj = camera.View * camera.Projection;
        var sunColor = sun.Colour * sun.Intensity;
        return new ForwardSceneConstants
        {
            ViewProjection = viewProj,
            SunDirection = new Vector4(Vector3.Normalize(sun.DirectionWorld), 0f),
            SunColor = new Vector4(sunColor, 0f),
            AmbientColor = new Vector4(ambientFallback, 0f),
            CameraPosition = new Vector4(camera.PositionWorld, 1f),
        };
    }
}
