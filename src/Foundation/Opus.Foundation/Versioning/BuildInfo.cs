using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Opus.Foundation;

/// <summary>
/// Immutable metadata describing the binary that produced the running process.
/// Captured once at startup via <see cref="Current"/> and stable for the rest of the
/// program lifetime. Crash-dump headers, log banners, and the in-game "About" panel
/// all read from this record so the binary signature is unambiguous regardless of where
/// it surfaces.
///
/// Ported from <c>svo::shared::BuildInfo</c>. The C++ version arrives via PRIVATE
/// CMake compile definitions baked at configure-time; .NET equivalents come from the
/// runtime — <see cref="Assembly.GetExecutingAssembly"/> for the version,
/// <see cref="RuntimeInformation"/> for OS / process architecture.
/// </summary>
public sealed record BuildInfo(
    EngineIdentity Engine,
    string ProjectName,
    AppVersion Version,
    string BuildConfiguration,
    string FrameworkDescription,
    string OperatingSystem,
    string ProcessArchitecture)
{
    /// <summary>The build info for the currently running process. Computed once on first
    /// access; the underlying introspection (assembly attributes, RuntimeInformation
    /// queries) is allocation-cheap but not free, so we cache it.</summary>
    public static BuildInfo Current { get; } = Capture();

    /// <summary>
    /// Human-readable one-line banner suitable for a log header or a crash-dump line.
    /// </summary>
    public string ToBannerLine() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Engine.ToIdentityLine()} [{BuildConfiguration}] assembly={ProjectName} assemblyVersion={Version} {FrameworkDescription} on {OperatingSystem} / {ProcessArchitecture}");

    public IReadOnlyList<string> ToReportLines() =>
        new[]
        {
            string.Create(CultureInfo.InvariantCulture, $"product: {Engine.DisplayName}"),
            string.Create(CultureInfo.InvariantCulture, $"productVersion: {Engine.ProductVersion}"),
            string.Create(CultureInfo.InvariantCulture, $"releaseChannel: {Engine.ReleaseChannel}"),
            string.Create(CultureInfo.InvariantCulture, $"assembly: {ProjectName}"),
            string.Create(CultureInfo.InvariantCulture, $"assemblyVersion: {Version}"),
            string.Create(CultureInfo.InvariantCulture, $"configuration: {BuildConfiguration}"),
            string.Create(CultureInfo.InvariantCulture, $"framework: {FrameworkDescription}"),
            string.Create(CultureInfo.InvariantCulture, $"os: {OperatingSystem}"),
            string.Create(CultureInfo.InvariantCulture, $"processArchitecture: {ProcessArchitecture}"),
            string.Create(CultureInfo.InvariantCulture, $"assemblyCompatibility: {Engine.AssemblyNamePrefix}"),
        };

    public string ToReportText() => string.Join(Environment.NewLine, ToReportLines());

    private static BuildInfo Capture()
    {
        var entry = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var name = entry.GetName().Name ?? "Opus";

        var versionString = entry
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        var version = string.IsNullOrWhiteSpace(versionString)
            ? AppVersion.Dev
            : ParseOrDev(versionString);

        var configuration = entry
            .GetCustomAttribute<AssemblyConfigurationAttribute>()
            ?.Configuration ?? "Unknown";

        return new BuildInfo(
            Engine: EngineIdentity.Current,
            ProjectName: name,
            Version: version,
            BuildConfiguration: configuration,
            FrameworkDescription: RuntimeInformation.FrameworkDescription,
            OperatingSystem: RuntimeInformation.OSDescription,
            ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString());
    }

    private static AppVersion ParseOrDev(string text)
    {
        try
        {
            return AppVersion.Parse(text);
        }
        catch (FormatException)
        {
            // AssemblyInformationalVersion can carry CI metadata that isn't strict semver
            // (e.g. "1.2.3+abc123-dirty"). Falling back to Dev keeps the boot path
            // robust against tooling churn — a CI fix is non-blocking.
            return AppVersion.Dev;
        }
    }
}
