using System;

namespace Opus.Engine.AlphaStress.Stress;

/// <summary>
/// Outcome of <see cref="AlphaStressReportWriter.Write"/>. Either paired JSON+TXT paths
/// or a structured <see cref="AlphaStressReportWriteIssue"/>; never both.
/// </summary>
public sealed record AlphaStressReportWriteResult(
    string? JsonPath,
    string? TextPath,
    AlphaStressReportWriteIssue? Issue)
{
    /// <summary>True when the writer persisted both files successfully.</summary>
    public bool IsSuccess => JsonPath is not null && TextPath is not null && Issue is null;

    /// <summary>Builds a successful outcome with paired paths.</summary>
    public static AlphaStressReportWriteResult Success(string jsonPath, string textPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(textPath);
        return new AlphaStressReportWriteResult(jsonPath, textPath, Issue: null);
    }

    /// <summary>Builds a failed outcome with a structured issue.</summary>
    public static AlphaStressReportWriteResult Failed(AlphaStressReportWriteIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return new AlphaStressReportWriteResult(JsonPath: null, TextPath: null, issue);
    }
}
