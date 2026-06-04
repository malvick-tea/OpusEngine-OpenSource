using System;
using System.IO;

namespace Opus.Foundation;

/// <summary>
/// Configuration for a session-scoped rolling file log sink. Values are validated up-front
/// (<see cref="Validate"/>) before the sink opens filesystem resources so the host fails
/// fast on misconfiguration instead of half-creating files.
/// </summary>
public sealed record RollingLogSinkOptions(
    string DirectoryPath,
    string FileNamePrefix,
    LogLevel MinimumLevel,
    long MaxFileBytes,
    int MaxTailEntries,
    RollingLogRetentionPolicy? Retention = null)
{
    /// <summary>Default file prefix used by Opus alpha hosts.</summary>
    public const string DefaultFileNamePrefix = "opus";

    /// <summary>Default maximum size for one log file before rolling (1 MiB).</summary>
    public const long DefaultMaxFileBytes = 1_048_576;

    /// <summary>Default number of entries retained in memory for failure reports.</summary>
    public const int DefaultMaxTailEntries = 256;

    /// <summary>Minimum size for a single rolled log file. Smaller values defeat rotation
    /// because the header itself can already exceed the cap.</summary>
    public const long MinimumMaxFileBytes = 4_096;

    /// <summary>Minimum number of tail entries retained in memory for failure reports.</summary>
    public const int MinimumTailEntries = 1;

    /// <summary>Names reserved by Windows for legacy device IO; using one as a log file
    /// prefix produces files that cannot be opened on the same machine.</summary>
    private static readonly string[] ReservedWindowsBaseNames =
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>Creates options with the Opus alpha defaults for a directory.</summary>
    public static RollingLogSinkOptions ForDirectory(string directoryPath) => new(
        directoryPath,
        DefaultFileNamePrefix,
        LogLevel.Information,
        DefaultMaxFileBytes,
        DefaultMaxTailEntries,
        Retention: null);

    /// <summary>Returns the active retention policy. Null falls back to
    /// <see cref="RollingLogRetentionPolicy.Disabled"/> so callers can read the policy
    /// without null checks.</summary>
    public RollingLogRetentionPolicy EffectiveRetention => Retention ?? RollingLogRetentionPolicy.Disabled;

    /// <summary>Validates option values before opening filesystem resources.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DirectoryPath))
        {
            throw new ArgumentException("Rolling log directory must not be empty.", nameof(DirectoryPath));
        }

        if (string.IsNullOrWhiteSpace(FileNamePrefix))
        {
            throw new ArgumentException("Rolling log file prefix must not be empty.", nameof(FileNamePrefix));
        }

        if (FileNamePrefix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException(
                "Rolling log file prefix contains invalid filename characters.",
                nameof(FileNamePrefix));
        }

        if (IsReservedWindowsName(FileNamePrefix))
        {
            throw new ArgumentException(
                "Rolling log file prefix matches a reserved Windows device name and cannot be opened on Windows.",
                nameof(FileNamePrefix));
        }

        if (MinimumLevel == LogLevel.None)
        {
            throw new ArgumentException(
                "Rolling log MinimumLevel must not be None — the sink would silently drop every entry.",
                nameof(MinimumLevel));
        }

        if (MaxFileBytes < MinimumMaxFileBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxFileBytes),
                $"MaxFileBytes must be at least {MinimumMaxFileBytes} bytes.");
        }

        if (MaxTailEntries < MinimumTailEntries)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxTailEntries),
                $"MaxTailEntries must be at least {MinimumTailEntries}.");
        }

        EffectiveRetention.Validate();
    }

    private static bool IsReservedWindowsName(string fileNamePrefix)
    {
        for (var i = 0; i < ReservedWindowsBaseNames.Length; i++)
        {
            if (string.Equals(fileNamePrefix, ReservedWindowsBaseNames[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
