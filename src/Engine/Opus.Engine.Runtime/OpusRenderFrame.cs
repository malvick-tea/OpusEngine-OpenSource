using System.Runtime.InteropServices;
using Opus.Foundation;

namespace Opus.Engine.Runtime;

/// <summary>Variable-frame snapshot delivered after fixed-tick catch-up.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct OpusRenderFrame(
    GameTime Time,
    TimeSpan Delta,
    double InterpolationAlpha,
    int FixedTicksExecuted,
    ulong FrameIndex);
