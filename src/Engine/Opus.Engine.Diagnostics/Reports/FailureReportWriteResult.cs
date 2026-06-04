namespace Opus.Engine.Diagnostics.Reports;

/// <summary>Result of writing a tester failure report to disk.</summary>
public sealed record FailureReportWriteResult(
    bool Succeeded,
    string? JsonPath,
    string? TextPath,
    FailureReportWriteIssue? Issue)
{
    /// <summary>Path of the screenshot copied next to the report bundle so the evidence is
    /// self-contained, or null when the report carried no screenshot or the best-effort
    /// attachment copy did not succeed. Distinct from <see cref="FailureReport.ScreenshotPath"/>,
    /// which records where the capture originally landed.</summary>
    public string? AttachedScreenshotPath { get; init; }

    /// <summary>Creates a successful write result, optionally carrying the path of a
    /// screenshot attached next to the bundle.</summary>
    public static FailureReportWriteResult Success(
        string jsonPath,
        string textPath,
        string? attachedScreenshotPath = null) => new(
        Succeeded: true,
        JsonPath: jsonPath,
        TextPath: textPath,
        Issue: null)
    {
        AttachedScreenshotPath = attachedScreenshotPath,
    };

    /// <summary>Creates a failed write result with one structured issue.</summary>
    public static FailureReportWriteResult Failed(FailureReportWriteIssue issue) => new(
        Succeeded: false,
        JsonPath: null,
        TextPath: null,
        Issue: issue);
}
