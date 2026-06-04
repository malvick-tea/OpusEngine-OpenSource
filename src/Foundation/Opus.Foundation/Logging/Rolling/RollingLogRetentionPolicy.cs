using System;

namespace Opus.Foundation;

/// <summary>
/// Retention policy applied by <see cref="RollingFileLogSink"/> when the sink opens a
/// fresh session. Sweeps existing log files matching the sink's prefix and deletes
/// anything that exceeds either the count limit or the age limit. Closes the M7 lead
/// follow-up "log + report retention cleanup".
/// </summary>
/// <param name="MaxFileCount">Maximum number of log files (including the file about to
/// open) the sink keeps under its directory. Older files past this limit are removed.
/// Zero disables the count rule.</param>
/// <param name="MaxAge">Maximum wall-clock age the sink keeps an existing log file for.
/// Files whose last-write time is older than <c>now - MaxAge</c> are removed.
/// <see cref="TimeSpan.Zero"/> disables the age rule.</param>
public sealed record RollingLogRetentionPolicy(int MaxFileCount, TimeSpan MaxAge)
{
    /// <summary>Default file-count limit (50 files) — comfortable for a tester slot
    /// that runs the alpha host multiple times per day.</summary>
    public const int DefaultMaxFileCount = 50;

    /// <summary>Default age limit (30 days) — long enough to cover a normal alpha
    /// reporting window without filling the disk with stale evidence.</summary>
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromDays(30);

    /// <summary>Default policy: 50 files, 30 days. Suits a tester machine running
    /// repeated alpha-host sessions.</summary>
    public static RollingLogRetentionPolicy Default { get; } = new(
        MaxFileCount: DefaultMaxFileCount,
        MaxAge: DefaultMaxAge);

    /// <summary>Disabled policy — every retention rule is a no-op. Equivalent to the
    /// pre-M12 behaviour where the sink never deleted prior session files.</summary>
    public static RollingLogRetentionPolicy Disabled { get; } = new(
        MaxFileCount: 0,
        MaxAge: TimeSpan.Zero);

    /// <summary>True when at least one retention rule is active.</summary>
    public bool IsActive => MaxFileCount > 0 || MaxAge > TimeSpan.Zero;

    /// <summary>Throws when the policy is internally inconsistent.</summary>
    public void Validate()
    {
        if (MaxFileCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxFileCount), "MaxFileCount must be non-negative.");
        }

        if (MaxAge < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxAge), "MaxAge must be non-negative.");
        }
    }
}
