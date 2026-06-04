namespace Opus.Engine.AlphaStress.KnownIssues;

/// <summary>
/// Triage tier for a long-lived known issue tracked through alpha. The roadmap M11
/// promise is "known issue list split into blocker / must-fix / post-alpha"; this enum
/// is the engine-neutral schema that encodes those buckets. Consumers (and the alpha
/// run report) treat each tier differently:
/// <list type="bullet">
///   <item><description><see cref="Blocker"/> — must be closed before the alpha ships.
///   An open blocker fails the stress run with <c>OPDX-STR-102</c>.</description></item>
///   <item><description><see cref="MustFix"/> — must be closed before release but does
///   not block alpha; surfaced as <c>OPDX-STR-103</c> warning.</description></item>
///   <item><description><see cref="PostAlpha"/> — acceptable to ship the alpha with
///   this open; recorded for triage only.</description></item>
/// </list>
/// Append-only — never reorder or repurpose values once the alpha ledger has shipped to
/// a tester machine.
/// </summary>
public enum KnownIssueSeverity
{
    /// <summary>Release blocker. Open value fails alpha-stress runs.</summary>
    Blocker = 0,

    /// <summary>Must fix before final release. Open value warns on alpha-stress runs.</summary>
    MustFix = 1,

    /// <summary>Triaged for post-alpha. Open value is informational only.</summary>
    PostAlpha = 2,
}
