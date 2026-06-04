namespace Opus.Engine.Renderer;

/// <summary>
/// Frame-wide post-processing parameters. Captured at <c>BeginFrame</c>, immutable
/// for the rest of the frame. Per ADR-0014's render path: HDR scene → tone map → bloom →
/// (optional) DOF / motion blur / colour grading → TAA → upscale → swap chain.
/// </summary>
public sealed record PostFxSetup(
    TonemapOperator Tonemap,
    BloomSetup Bloom,
    ColourGradingSetup ColourGrading,
    AntiAliasingMode AntiAliasing,
    UpscaleMode Upscale,
    float ExposureEv);

public enum TonemapOperator : byte
{
    None = 0,
    Reinhard = 1,

    /// <summary>ACES Filmic (Narkowicz approximation). Default — see ADR-0025.</summary>
    AcesFilmic = 2,
    Aces = 3,
    Uncharted2 = 4,
}

public readonly record struct BloomSetup(
    bool Enabled,
    float Threshold,
    float Intensity,
    int MipChainLevels);

public readonly record struct ColourGradingSetup(
    bool Enabled,
    int LutHandle,
    float Saturation,
    float Contrast);

public enum AntiAliasingMode : byte
{
    None = 0,
    Fxaa = 1,
    Taa = 2,
    Smaa = 3,
}

/// <summary>Upscaling. <see cref="None"/> means native render resolution = output
/// resolution. Other modes integrate with vendor libraries (DLSS / FSR2 / XeSS) — opt-in
/// per ADR-0014, fallback to render-at-resolution when unavailable.</summary>
public enum UpscaleMode : byte
{
    None = 0,
    Dlss = 1,
    Fsr2 = 2,
    Xess = 3,
}
