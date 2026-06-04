using System;

namespace Opus.Engine.Diagnostics.Reports;

/// <summary>
/// Retention policy applied to paired-artifact diagnostics outputs (failure reports,
/// alpha smoke reports, alpha stress reports). Each artifact is written as a JSON +
/// text pair sharing a stem; the policy treats the pair as the retention unit so a
/// half-deleted pair never lingers on disk. Closes the M7 + M11 lead follow-up
/// "log + report retention cleanup".
/// </summary>
/// <param name="MaxPairCount">Maximum number of artifact pairs the writer keeps under
/// its directory. Older pairs past this limit are removed before the next write.
/// Zero disables the count rule.</param>
/// <param name="MaxAge">Maximum wall-clock age a pair is retained for, measured against
/// the newer of the two files' last-write time. <see cref="TimeSpan.Zero"/> disables
/// the age rule.</param>
public sealed record DiagnosticsArtifactRetentionPolicy(int MaxPairCount, TimeSpan MaxAge)
{
    /// <summary>Default pair-count limit (50 pairs) — comfortable for a tester slot
    /// emitting failure / smoke / stress evidence across an alpha shift.</summary>
    public const int DefaultMaxPairCount = 50;

    /// <summary>Default age limit (30 days) — matches the rolling log default so
    /// log and report retention age out together for the same tester window.</summary>
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromDays(30);

    /// <summary>Default policy: 50 pairs, 30 days. Sized for tester machines running
    /// repeated alpha-host sessions.</summary>
    public static DiagnosticsArtifactRetentionPolicy Default { get; } = new(
        MaxPairCount: DefaultMaxPairCount,
        MaxAge: DefaultMaxAge);

    /// <summary>Disabled policy — every retention rule is a no-op. Preserves the
    /// pre-M11.3 behaviour where the writer never deleted prior artifacts.</summary>
    public static DiagnosticsArtifactRetentionPolicy Disabled { get; } = new(
        MaxPairCount: 0,
        MaxAge: TimeSpan.Zero);

    /// <summary>True when at least one retention rule is active.</summary>
    public bool IsActive => MaxPairCount > 0 || MaxAge > TimeSpan.Zero;

    /// <summary>Throws when the policy is internally inconsistent.</summary>
    public void Validate()
    {
        if (MaxPairCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxPairCount), "MaxPairCount must be non-negative.");
        }

        if (MaxAge < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxAge), "MaxAge must be non-negative.");
        }
    }
}
