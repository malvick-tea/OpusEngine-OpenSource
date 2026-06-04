using System.Numerics;
using Opus.Engine.Renderer;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Builds an <see cref="IRenderer.BeginFrame"/>-ready <see cref="FrameCameraSet"/>
/// from an <see cref="OrbitCamera"/> snapshot. Lifts the near/far/FOV/aspect plumbing into
/// one place so every consumer of the orbit camera produces a consistent
/// <see cref="CameraSetup"/>.</summary>
public static class OrbitFrameCameraBuilder
{
    public static FrameCameraSet Build(
        in OrbitCamera orbit,
        in Matrix4x4 view,
        in Vector3 cameraPos)
    {
        var forward = Vector3.Normalize(orbit.Centre - cameraPos);
        return FrameCameraSet.SingleMain(new CameraSetup(
            View: view,
            Projection: orbit.Projection,
            PositionWorld: cameraPos,
            ForwardWorld: forward,
            NearPlane: orbit.NearPlane,
            FarPlane: orbit.FarPlane,
            FovYRadians: orbit.FovYRadians,
            AspectRatio: orbit.AspectRatio));
    }
}
