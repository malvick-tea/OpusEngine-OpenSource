using System;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Opus.Engine.Diagnostics.Reports;
using Opus.Foundation;

namespace Opus.Engine.AlphaHarness.Smoke;

/// <summary>
/// Writes alpha smoke-run evidence as paired JSON and text artifacts. Mirrors the M7
/// failure-report writer shape: lock-protected, atomic per file (write-to-temp then
/// replace), narrow filesystem exception classification, and never throws past the public
/// API. The shape lets failure-report and smoke evidence cross-reference each other from
/// the same diagnostics root.
/// </summary>
public sealed class AlphaSmokeReportWriter
{
    private const string JsonExtension = ".json";
    private const string TextExtension = ".txt";
    private const string TempExtension = ".tmp";
    private const string RowSeparator = ": ";
    private const string TextIndent = "  ";
    private const string TextHeader = "Opus alpha smoke report";
    private const string MissingScreenshotLabel = "none";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _writeLock = new();
    private readonly AlphaSmokeReportWriterOptions _options;
    private readonly TimeProvider _clock;

    /// <summary>Creates a writer with explicit filesystem options.</summary>
    public AlphaSmokeReportWriter(AlphaSmokeReportWriterOptions options)
        : this(options, TimeProvider.System)
    {
    }

    /// <summary>Test-friendly constructor with an explicit clock for retention sweeps.</summary>
    public AlphaSmokeReportWriter(AlphaSmokeReportWriterOptions options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _options = options;
        _clock = clock;
    }

    /// <summary>Writes an outcome and returns paired paths or a structured issue.</summary>
    public AlphaSmokeReportWriteResult Write(AlphaSmokeOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        try
        {
            _options.Validate();
        }
        catch (ArgumentException ex)
        {
            return AlphaSmokeReportWriteResult.Failed(BuildIssue(
                AlphaHarnessDiagnosticCodes.SmokeReportWriteFailed,
                ex,
                "Check that the smoke report directory option is non-empty before invoking the writer."));
        }

        lock (_writeLock)
        {
            return WriteCore(outcome);
        }
    }

    private AlphaSmokeReportWriteResult WriteCore(AlphaSmokeOutcome outcome)
    {
        try
        {
            Directory.CreateDirectory(_options.DirectoryPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            return AlphaSmokeReportWriteResult.Failed(BuildIssue(
                AlphaHarnessDiagnosticCodes.SmokeReportWriteFailed,
                ex,
                "Grant write permission to the diagnostics smoke directory or relocate it."));
        }
        catch (Exception ex) when (IsFilesystemException(ex))
        {
            return AlphaSmokeReportWriteResult.Failed(BuildIssue(
                AlphaHarnessDiagnosticCodes.SmokeReportWriteFailed,
                ex,
                "Smoke directory could not be created. Free disk space or fix path validity."));
        }

        DiagnosticsArtifactRetentionSweeper.Sweep(
            _options.DirectoryPath,
            AlphaSmokeReportWriterOptions.ArtifactStemPrefix,
            _options.EffectiveRetention,
            _clock.GetUtcNow());

        var stem = BuildFileStem(outcome);
        var jsonPath = Path.Combine(_options.DirectoryPath, stem + JsonExtension);
        var textPath = Path.Combine(_options.DirectoryPath, stem + TextExtension);

        try
        {
            WriteAtomic(jsonPath, JsonSerializer.Serialize(outcome, JsonOptions));
            WriteAtomic(textPath, BuildText(outcome));
        }
        catch (UnauthorizedAccessException ex)
        {
            return AlphaSmokeReportWriteResult.Failed(BuildIssue(
                AlphaHarnessDiagnosticCodes.SmokeReportWriteFailed,
                ex,
                "Grant write permission to the diagnostics smoke directory."));
        }
        catch (JsonException ex)
        {
            return AlphaSmokeReportWriteResult.Failed(BuildIssue(
                AlphaHarnessDiagnosticCodes.SmokeReportWriteFailed,
                ex,
                "Smoke outcome failed to serialise. Capture stack trace and report to engine lead."));
        }
        catch (Exception ex) when (IsFilesystemException(ex))
        {
            return AlphaSmokeReportWriteResult.Failed(BuildIssue(
                AlphaHarnessDiagnosticCodes.SmokeReportWriteFailed,
                ex,
                "Smoke report write failed. Free disk space or relocate the diagnostics root."));
        }

        return AlphaSmokeReportWriteResult.Success(jsonPath, textPath);
    }

    private static void WriteAtomic(string finalPath, string content)
    {
        var tempPath = finalPath + TempExtension;
        File.WriteAllText(tempPath, content, Encoding.UTF8);
        if (File.Exists(finalPath))
        {
            File.Replace(tempPath, finalPath, destinationBackupFileName: null);
            return;
        }

        File.Move(tempPath, finalPath);
    }

    private AlphaSmokeReportWriteIssue BuildIssue(string code, Exception exception, string remediation) => new(
        Code: code,
        Severity: LogLevel.Error,
        Path: _options.DirectoryPath,
        Message: string.Create(
            CultureInfo.InvariantCulture,
            $"Alpha smoke report write failed for '{_options.DirectoryPath}': {exception.GetType().Name}: {exception.Message}"),
        RemediationHint: remediation);

    private static bool IsFilesystemException(Exception exception) =>
        exception is IOException
            or SecurityException
            or NotSupportedException
            or PathTooLongException
            or DirectoryNotFoundException;

    private static string BuildFileStem(AlphaSmokeOutcome outcome) => string.Create(
        CultureInfo.InvariantCulture,
        $"opus-alpha-smoke-{outcome.StartedAtUtc:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}");

    private static string BuildText(AlphaSmokeOutcome outcome)
    {
        var text = new StringBuilder();
        text.AppendLine(TextHeader);
        AppendRow(text, "smokeName", outcome.Profile.SmokeName);
        AppendRow(text, "startedAtUtc", outcome.StartedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        AppendRow(text, "elapsedMs", outcome.ElapsedWallClock.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture));
        AppendRow(text, "framesStepped", outcome.FramesStepped.ToString(CultureInfo.InvariantCulture));
        AppendRow(text, "frameTarget", outcome.Profile.FrameTarget.ToString(CultureInfo.InvariantCulture));
        AppendRow(text, "meanCpuMs", outcome.MeanCpuFrameTime.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture));
        AppendRow(text, "p95CpuMs", outcome.P95CpuFrameTime.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture));
        AppendRow(text, "maxCpuMs", outcome.MaxCpuFrameTime.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture));
        AppendRow(text, "screenshot", outcome.ScreenshotPath ?? MissingScreenshotLabel);
        AppendRow(text, "isClean", outcome.IsClean ? "true" : "false");
        AppendIssues(text, outcome);
        return text.ToString();
    }

    private static void AppendRow(StringBuilder text, string label, string value)
    {
        text.Append(label);
        text.Append(RowSeparator);
        text.AppendLine(value);
    }

    private static void AppendIssues(StringBuilder text, AlphaSmokeOutcome outcome)
    {
        text.AppendLine("issues:");
        if (outcome.Issues.Count == 0)
        {
            text.Append(TextIndent);
            text.AppendLine(MissingScreenshotLabel);
            return;
        }

        foreach (var issue in outcome.Issues)
        {
            text.Append(TextIndent);
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{issue.DiagnosticCode} {issue.ObservedAtUtc:O} {issue.Message}"));
        }
    }
}
