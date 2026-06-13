namespace Opus.Engine.AlphaHarness.Machine;

/// <summary>Coarse-grained operating system family the machine profile distinguishes.
/// Production Opus 0.1 targets Windows; other families are recorded so a CI host can
/// capture its profile without being rejected outright.</summary>
public enum MachineOperatingSystemFamily
{
    /// <summary>Reporter could not classify the OS into a known family.</summary>
    Unknown,

    /// <summary>Windows family. Opus 0.1 alpha target.</summary>
    Windows,

    /// <summary>Linux family. Build target for headless CI only.</summary>
    Linux,

    /// <summary>macOS family. Build target for headless CI only.</summary>
    MacOs,
}
