namespace Opus.Engine.AlphaHarness.Smoke;

/// <summary>Outcome of <see cref="AlphaSmokeReportWriter.Write"/>. Returns either a
/// success pair of paths or a structured <see cref="AlphaSmokeReportWriteIssue"/>; the
/// writer never throws past this boundary so a host loop can record evidence even when
/// the filesystem is hostile.</summary>
public sealed record AlphaSmokeReportWriteResult
{
    private AlphaSmokeReportWriteResult(
        bool succeeded,
        string? jsonPath,
        string? textPath,
        AlphaSmokeReportWriteIssue? issue)
    {
        Succeeded = succeeded;
        JsonPath = jsonPath;
        TextPath = textPath;
        Issue = issue;
    }

    /// <summary>True when both JSON and TXT artifacts were persisted.</summary>
    public bool Succeeded { get; }

    /// <summary>Absolute path to the JSON artifact when <see cref="Succeeded"/>.</summary>
    public string? JsonPath { get; }

    /// <summary>Absolute path to the TXT artifact when <see cref="Succeeded"/>.</summary>
    public string? TextPath { get; }

    /// <summary>Structured failure issue when <see cref="Succeeded"/> is false.</summary>
    public AlphaSmokeReportWriteIssue? Issue { get; }

    /// <summary>Builds a success result from the two resolved paths.</summary>
    public static AlphaSmokeReportWriteResult Success(string jsonPath, string textPath) =>
        new(true, jsonPath, textPath, null);

    /// <summary>Builds a failed result with the supplied diagnostic issue.</summary>
    public static AlphaSmokeReportWriteResult Failed(AlphaSmokeReportWriteIssue issue) =>
        new(false, null, null, issue);
}
