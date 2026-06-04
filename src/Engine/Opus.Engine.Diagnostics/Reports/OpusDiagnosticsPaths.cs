using System;
using System.IO;
using System.Security;
using Opus.Foundation;

namespace Opus.Engine.Diagnostics.Reports;

/// <summary>
/// Standard filesystem locations for Opus tester diagnostics artifacts. The product
/// directory name is sourced from <see cref="EngineIdentity.Current"/> so the path moves
/// with the public identity instead of a duplicated string literal. All derived helpers
/// fully resolve their root with <see cref="Path.GetFullPath(string)"/> to neutralise
/// caller-supplied <c>..</c> traversal segments before any IO opens a handle.
/// <para>
/// The default root honours the <see cref="RootDirectoryEnvironmentVariable"/> environment
/// variable so a tester-machine installer or CI runner can configure one persistent
/// evidence location for every Opus tool without a per-invocation flag. Precedence is:
/// an explicit caller-supplied directory (e.g. the <c>--diagnostics-dir</c> CLI option) &gt;
/// the environment variable &gt; the per-user local-application-data location.
/// </para>
/// </summary>
public static class OpusDiagnosticsPaths
{
    /// <summary>Subdirectory name shared by every Opus diagnostics layout.</summary>
    public const string DiagnosticsDirectoryName = "diagnostics";

    /// <summary>Environment variable that, when set to a usable path, overrides the default
    /// per-user diagnostics root. The value is treated as the diagnostics root directly
    /// (the same shape as the <c>--diagnostics-dir</c> CLI option), so logs / reports /
    /// smoke / stress / known-issues land directly beneath it. A blank or malformed value
    /// is ignored and the per-user location is used instead — a misconfigured installer
    /// never deprives a tester of an evidence directory.</summary>
    public const string RootDirectoryEnvironmentVariable = "OPUS_DIAGNOSTICS_DIR";

    /// <summary>Subdirectory name used for rolling text logs.</summary>
    public const string LogsDirectoryName = "logs";

    /// <summary>Subdirectory name used for tester failure-report bundles.</summary>
    public const string ReportsDirectoryName = "reports";

    /// <summary>Subdirectory name used for alpha smoke-run evidence bundles.</summary>
    public const string SmokeDirectoryName = "smoke";

    /// <summary>Subdirectory name used for alpha stress-run evidence bundles.</summary>
    public const string StressDirectoryName = "stress";

    /// <summary>Subdirectory name used for the long-lived known-issue ledger.</summary>
    public const string KnownIssuesDirectoryName = "known-issues";

    /// <summary>Subdirectory name used for tester-captured PNG screenshots.</summary>
    public const string ScreenshotsDirectoryName = "screenshots";

    /// <summary>Returns the default diagnostics root directory. When the
    /// <see cref="RootDirectoryEnvironmentVariable"/> environment variable holds a usable
    /// path it is used as the root; otherwise the root is built under the user's local
    /// application-data folder (falling back to the user profile, then the current
    /// directory) so a host environment with a pruned profile still has a stable,
    /// persistent location for evidence.</summary>
    public static string DefaultRootDirectory() =>
        ResolveRootDirectory(Environment.GetEnvironmentVariable(RootDirectoryEnvironmentVariable));

    /// <summary>Resolves a diagnostics root from an optional caller-configured value
    /// (typically the <see cref="RootDirectoryEnvironmentVariable"/> value or a host
    /// configuration entry). A non-blank value that resolves to a full path is used as the
    /// root directly; a null, blank, or malformed value falls back to the per-user
    /// local-application-data location. Pure — no environment or filesystem state is read,
    /// so the precedence is deterministically testable.</summary>
    public static string ResolveRootDirectory(string? configuredRoot)
    {
        if (!string.IsNullOrWhiteSpace(configuredRoot) && TryResolveFullPath(configuredRoot, out var configuredFullPath))
        {
            return configuredFullPath;
        }

        var basePath = ResolveDefaultBaseDirectory();
        return Path.GetFullPath(Path.Combine(basePath, EngineIdentity.Current.ProductName, DiagnosticsDirectoryName));
    }

    /// <summary>Returns the default rolling-log directory.</summary>
    public static string DefaultLogsDirectory() =>
        LogsDirectory(DefaultRootDirectory());

    /// <summary>Returns the default failure-report directory.</summary>
    public static string DefaultReportsDirectory() =>
        ReportsDirectory(DefaultRootDirectory());

    /// <summary>Returns the default alpha smoke-evidence directory.</summary>
    public static string DefaultSmokeDirectory() =>
        SmokeDirectory(DefaultRootDirectory());

    /// <summary>Returns the default alpha stress-evidence directory.</summary>
    public static string DefaultStressDirectory() =>
        StressDirectory(DefaultRootDirectory());

    /// <summary>Returns the default known-issue ledger directory.</summary>
    public static string DefaultKnownIssuesDirectory() =>
        KnownIssuesDirectory(DefaultRootDirectory());

    /// <summary>Returns the default tester-screenshot directory.</summary>
    public static string DefaultScreenshotsDirectory() =>
        ScreenshotsDirectory(DefaultRootDirectory());

    /// <summary>Returns the rolling-log directory below a diagnostics root.</summary>
    public static string LogsDirectory(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        return Path.GetFullPath(Path.Combine(rootDirectory, LogsDirectoryName));
    }

    /// <summary>Returns the failure-report directory below a diagnostics root.</summary>
    public static string ReportsDirectory(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        return Path.GetFullPath(Path.Combine(rootDirectory, ReportsDirectoryName));
    }

    /// <summary>Returns the alpha smoke-evidence directory below a diagnostics root.</summary>
    public static string SmokeDirectory(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        return Path.GetFullPath(Path.Combine(rootDirectory, SmokeDirectoryName));
    }

    /// <summary>Returns the alpha stress-evidence directory below a diagnostics root.</summary>
    public static string StressDirectory(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        return Path.GetFullPath(Path.Combine(rootDirectory, StressDirectoryName));
    }

    /// <summary>Returns the known-issue ledger directory below a diagnostics root.</summary>
    public static string KnownIssuesDirectory(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        return Path.GetFullPath(Path.Combine(rootDirectory, KnownIssuesDirectoryName));
    }

    /// <summary>Returns the tester-screenshot directory below a diagnostics root.</summary>
    public static string ScreenshotsDirectory(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        return Path.GetFullPath(Path.Combine(rootDirectory, ScreenshotsDirectoryName));
    }

    private static string ResolveDefaultBaseDirectory()
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return localAppData;
        }

        var userProfile = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile,
            Environment.SpecialFolderOption.DoNotVerify);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            return userProfile;
        }

        // In environments without a usable user profile (locked-down CI containers, etc.)
        // the diagnostics directory falls back to the process working directory. A
        // deployment that needs a fixed location sets RootDirectoryEnvironmentVariable,
        // which is honoured ahead of this per-user chain by ResolveRootDirectory.
        return Directory.GetCurrentDirectory();
    }

    private static bool TryResolveFullPath(string path, out string fullPath)
    {
        try
        {
            fullPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException or SecurityException)
        {
            fullPath = string.Empty;
            return false;
        }
    }
}
