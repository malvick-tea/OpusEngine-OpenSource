using System;

namespace Opus.Engine.AlphaHarness.Machine;

/// <summary>
/// Snapshot of the engine-visible parts of a host machine — what a captured baseline
/// records and what a fresh capture is compared against. The shape is deliberately small
/// and explicit: every field is something Opus tester evidence already references (build
/// banner, failure reports, screenshot metadata), and nothing is game-specific.
/// </summary>
/// <param name="ProfileName">Display name for the profile, e.g. "windows-d3d12-2026".</param>
/// <param name="OperatingSystemFamily">Coarse OS family classification.</param>
/// <param name="OperatingSystemDescription">Verbatim <c>RuntimeInformation.OSDescription</c>
/// at capture time.</param>
/// <param name="ProcessArchitecture">Architecture the host process is running under.</param>
/// <param name="LogicalProcessorCount">Number of logical processors visible to the runtime.</param>
/// <param name="DotnetRuntimeVersion">The dotnet runtime banner string used by the host.</param>
/// <param name="GraphicsAdapterName">Name of the live D3D12 adapter, or null when the
/// machine did not expose one at capture time (CI host, headless container).</param>
/// <param name="CapturedAtUtc">UTC timestamp at which the profile was captured.</param>
/// <param name="Notes">Free-form text the operator attached when authoring a known-good
/// reference (e.g. driver version, GPU memory, validated tester scenarios). Null is fine.</param>
public sealed record KnownGoodMachineProfile(
    string ProfileName,
    MachineOperatingSystemFamily OperatingSystemFamily,
    string OperatingSystemDescription,
    string ProcessArchitecture,
    int LogicalProcessorCount,
    string DotnetRuntimeVersion,
    string? GraphicsAdapterName,
    DateTimeOffset CapturedAtUtc,
    string? Notes)
{
    /// <summary>Throws when the profile shape is internally inconsistent. Comparison
    /// callers validate before they diff so a malformed reference fails loudly.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProfileName))
        {
            throw new ArgumentException("ProfileName must not be empty.", nameof(ProfileName));
        }

        if (string.IsNullOrWhiteSpace(OperatingSystemDescription))
        {
            throw new ArgumentException("OperatingSystemDescription must not be empty.", nameof(OperatingSystemDescription));
        }

        if (string.IsNullOrWhiteSpace(ProcessArchitecture))
        {
            throw new ArgumentException("ProcessArchitecture must not be empty.", nameof(ProcessArchitecture));
        }

        if (LogicalProcessorCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(LogicalProcessorCount), "LogicalProcessorCount must be at least 1.");
        }

        if (string.IsNullOrWhiteSpace(DotnetRuntimeVersion))
        {
            throw new ArgumentException("DotnetRuntimeVersion must not be empty.", nameof(DotnetRuntimeVersion));
        }
    }
}
