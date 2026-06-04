using System.Runtime.InteropServices;
using Opus.Engine.Pal.Application;

namespace Opus.Engine.Pal.Windows.Direct3D12;

/// <summary>
/// Immutable creation parameters for a <see cref="D3D12WindowSession"/>. Separates the
/// SDL window descriptor (title / dimensions / mode) from the D3D12-specific knobs
/// (debug-layer enablement) so the underlying <see cref="WindowOptions"/> stays
/// platform-agnostic and the D3D12 layer owns the GPU-side switches.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct D3D12WindowSessionOptions(
    WindowOptions Window,
    bool EnableDebugLayer)
{
    /// <summary>Default windowed 1280×720, no debug layer, no vsync — the runtime
    /// bring-up used by Garage.Demo. Smoke tests override the debug-layer flag.
    /// <paramref name="resizable"/> defaults to <c>false</c> to preserve fixed-size callers
    /// (the demo and deterministic smokes); the live alpha host passes <c>true</c>.</summary>
    public static D3D12WindowSessionOptions Windowed(string title, int width, int height, bool enableDebugLayer = false, bool resizable = false) =>
        new(new WindowOptions(title, width, height, Resizable: resizable, VSync: false, WindowMode.Windowed), enableDebugLayer);
}
