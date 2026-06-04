namespace Opus.Engine.Pal.Application;

/// <summary>
/// Stable codes for OS application-level events. We don't propagate the raw OS message
/// because (a) they differ per platform and (b) consumers should never branch on platform.
/// </summary>
public enum ApplicationEvent
{
    Launched,
    BecameForeground,
    BecameBackground,
    LowMemoryWarning,
    ThermalStateChanged,
    OrientationChanged,
    LocaleChanged,
    ShutdownRequested,
}
