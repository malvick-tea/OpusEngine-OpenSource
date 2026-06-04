namespace Opus.Foundation;

/// <summary>
/// Three-letter <see cref="LogLevel"/> tags shared by the rolling log sink and the
/// rolling log entry formatter. Kept internal: only the rolling-file family needs the
/// tags; <see cref="ConsoleLog"/> owns its own tag table because it deliberately does
/// not couple to file-based formatting.
/// </summary>
internal static class RollingLogLevelTags
{
    /// <summary>Tag used when a level value falls outside the <see cref="LogLevel"/>
    /// enum range. Falls back to <see cref="LogLevel.Information"/> semantics.</summary>
    public const string FallbackTag = "INF";

    private static readonly string[] Tags =
    {
        "TRC",
        "DBG",
        "INF",
        "WRN",
        "ERR",
        "CRT",
        "OFF",
    };

    /// <summary>Returns the three-letter tag for a level, or <see cref="FallbackTag"/>
    /// for an out-of-range level value.</summary>
    public static string ToTag(LogLevel level)
    {
        var index = (int)level;
        return index >= 0 && index < Tags.Length ? Tags[index] : FallbackTag;
    }
}
