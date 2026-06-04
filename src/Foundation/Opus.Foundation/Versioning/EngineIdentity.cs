namespace Opus.Foundation;

/// <summary>
/// Public product identity for the engine, now aligned with the Opus.* assembly names (ADR-0036).
/// </summary>
public sealed record EngineIdentity(
    string ProductName,
    AppVersion ProductVersion,
    string DisplayVersion,
    string ReleaseChannel,
    string AssemblyNamePrefix,
    string AssemblyCompatibility)
{
    private const string CurrentProductName = "Opus";
    private const string CurrentDisplayVersion = "0.1";
    private const string CurrentReleaseChannel = "alpha";
    private const string CurrentAssemblyNamePrefix = "Opus.*";
    private const string CurrentAssemblyCompatibility =
        "Assembly names were normalised to the canonical Opus.* namespace (ADR-0036).";

    /// <summary>
    /// Lead release point: the canonical Opus.* assembly-name normalisation landed via
    /// ADR-0036. Update this when Opus 0.1 leaves alpha.
    /// </summary>
    public static EngineIdentity Current { get; } = new(
        CurrentProductName,
        new AppVersion(0, 1, 0, CurrentReleaseChannel, string.Empty),
        CurrentDisplayVersion,
        CurrentReleaseChannel,
        CurrentAssemblyNamePrefix,
        CurrentAssemblyCompatibility);

    public string DisplayName => $"{ProductName} {DisplayVersion}";

    public string ToIdentityLine() => $"{DisplayName} ({ProductVersion})";
}
