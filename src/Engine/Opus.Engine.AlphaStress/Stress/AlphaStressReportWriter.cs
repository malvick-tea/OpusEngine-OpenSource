using System;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Opus.Engine.Diagnostics.Reports;
using Opus.Foundation;
using Opus.Foundation.IO;

namespace Opus.Engine.AlphaStress.Stress;

/// <summary>
/// Writes alpha stress-run evidence as paired JSON and text artifacts. Mirrors the M9
/// smoke-report-writer shape: lock-protected, atomic per file (write-to-temp then
/// replace), narrow filesystem exception classification, never throws past the public
/// API. The shape lets stress, smoke, and failure-report evidence cross-reference each
/// other from the same diagnostics root.
/// </summary>
public sealed class AlphaStressReportWriter
{
    private const string JsonExtension = ".json";
    private const string TextExtension = ".txt";
    private const string RowSeparator = ": ";
    private const string TextIndent = "  ";
    private const string TextHeader = "Opus alpha stress report";
    private const string MissingLabel = "none";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _writeLock = new();
    private readonly AlphaStressReportWriterOptions _options;
    private readonly TimeProvider _clock;

    /// <summary>Creates a writer with explicit filesystem options.</summary>
    public AlphaStressReportWriter(AlphaStressReportWriterOptions options)
        : this(options, TimeProvider.System)
    {
    }

    /// <summary>Test-friendly constructor with an explicit clock for retention sweeps.</summary>
    public AlphaStressReportWriter(AlphaStressReportWriterOptions options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _options = options;
        _clock = clock;
    }

    /// <summary>Writes a stress outcome and returns paired paths or a structured issue.</summary>
    public AlphaStressReportWriteResult Write(AlphaStressOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        try
        {
            _options.Validate();
        }
        catch (ArgumentException ex)
        {
            return AlphaStressReportWriteResult.Failed(BuildIssue(
                ex,
                "Configure a non-empty stress directory option before invoking the writer."));
        }

        lock (_writeLock)
        {
            return WriteCore(outcome);
        }
    }

    private AlphaStressReportWriteResult WriteCore(AlphaStressOutcome outcome)
    {
        try
        {
            Directory.CreateDirectory(_options.DirectoryPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            return AlphaStressReportWriteResult.Failed(BuildIssue(
                ex,
                "Grant write permission to the diagnostics stress directory or relocate it."));
        }
        catch (Exception ex) when (IsFilesystemException(ex))
        {
            return AlphaStressReportWriteResult.Failed(BuildIssue(
                ex,
                "Stress directory could not be created. Free disk space or fix path validity."));
        }

        DiagnosticsArtifactRetentionSweeper.Sweep(
            _options.DirectoryPath,
            AlphaStressReportWriterOptions.ArtifactStemPrefix,
            _options.EffectiveRetention,
            _clock.GetUtcNow());

        var stem = BuildFileStem(outcome);
        var jsonPath = Path.Combine(_options.DirectoryPath, stem + JsonExtension);
        var textPath = Path.Combine(_options.DirectoryPath, stem + TextExtension);

        try
        {
            AtomicFile.WriteAllText(jsonPath, JsonSerializer.Serialize(outcome, JsonOptions));
            AtomicFile.WriteAllText(textPath, BuildText(outcome));
        }
        catch (UnauthorizedAccessException ex)
        {
            return AlphaStressReportWriteResult.Failed(BuildIssue(
                ex,
                "Grant write permission to the diagnostics stress directory."));
        }
        catch (JsonException ex)
        {
            return AlphaStressReportWriteResult.Failed(BuildIssue(
                ex,
                "Stress outcome failed to serialise. Capture stack trace and report to engine lead."));
        }
        catch (Exception ex) when (IsFilesystemException(ex))
        {
            return AlphaStressReportWriteResult.Failed(BuildIssue(
                ex,
                "Stress report write failed. Free disk space or relocate the diagnostics root."));
        }

        return AlphaStressReportWriteResult.Success(jsonPath, textPath);
    }

    private AlphaStressReportWriteIssue BuildIssue(Exception exception, string remediation) => new(
        Code: AlphaStressDiagnosticCodes.StressReportWriteFailed,
        Severity: LogLevel.Error,
        Path: _options.DirectoryPath,
        Message: string.Create(
            CultureInfo.InvariantCulture,
            $"Alpha stress report write failed for '{_options.DirectoryPath}': {exception.GetType().Name}: {exception.Message}"),
        RemediationHint: remediation);

    private static bool IsFilesystemException(Exception exception) =>
        exception is IOException
            or SecurityException
            or NotSupportedException
            or PathTooLongException
            or DirectoryNotFoundException;

    private static string BuildFileStem(AlphaStressOutcome outcome) => string.Create(
        CultureInfo.InvariantCulture,
        $"opus-alpha-stress-{outcome.StartedAtUtc:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}");

    private static string BuildText(AlphaStressOutcome outcome)
    {
        var text = new StringBuilder();
        text.AppendLine(TextHeader);
        AppendRow(text, "stressName", outcome.Profile.StressName);
        AppendRow(text, "startedAtUtc", outcome.StartedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        AppendRow(text, "elapsedMs", outcome.ElapsedWallClock.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture));
        AppendRow(text, "iterationsCompleted", outcome.Iterations.Count.ToString(CultureInfo.InvariantCulture));
        AppendRow(text, "iterationsRequested", outcome.Profile.IterationCount.ToString(CultureInfo.InvariantCulture));
        AppendRow(text, "isClean", outcome.IsClean ? "true" : "false");
        AppendRow(text, "allIterationsClean", outcome.AllIterationsClean ? "true" : "false");
        AppendFramePacing(text, outcome);
        AppendMemory(text, outcome);
        AppendNetwork(text, outcome);
        AppendIssues(text, outcome);
        return text.ToString();
    }

    private static void AppendFramePacing(StringBuilder text, AlphaStressOutcome outcome)
    {
        text.AppendLine("framePacing:");
        var pacing = outcome.AggregatedFramePacing;
        AppendIndented(text, "samples", pacing.SampleCount.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "meanMs", pacing.Mean.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture));
        AppendIndented(text, "p95Ms", pacing.Percentile95.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture));
        AppendIndented(text, "p99Ms", pacing.Percentile99.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture));
        AppendIndented(text, "maxMs", pacing.Max.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture));
        AppendIndented(text, "hitchCount", pacing.HitchCount.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendMemory(StringBuilder text, AlphaStressOutcome outcome)
    {
        text.AppendLine("memory:");
        var memory = outcome.MemoryProbe;
        AppendIndented(text, "samples", memory.SampleCount.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "managedGrowthBytes", memory.ManagedHeapGrowthBytes.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "workingSetGrowthBytes", memory.WorkingSetGrowthBytes.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "peakWorkingSetBytes", memory.PeakWorkingSetBytes.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "gen2Delta", memory.Gen2CollectionsDelta.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendNetwork(StringBuilder text, AlphaStressOutcome outcome)
    {
        text.AppendLine("network:");
        var network = outcome.Network;
        AppendIndented(text, "iterations", network.IterationCount.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "clientSendAttempts", network.TotalClientSendAttempts.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "droppedPackets", network.TotalDroppedPackets.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "delayedPackets", network.TotalDelayedPackets.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "soakIssueCount", network.TotalSoakIssueCount.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "dropFraction", network.DropFraction.ToString("F3", CultureInfo.InvariantCulture));
        AppendIndented(text, "delayedFraction", network.DelayedFraction.ToString("F3", CultureInfo.InvariantCulture));
        AppendIndented(text, "inboundAttempts", network.TotalInboundAttempts.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "inboundDroppedPackets", network.TotalInboundDroppedPackets.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "inboundDelayedPackets", network.TotalInboundDelayedPackets.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "inboundDropFraction", network.InboundDropFraction.ToString("F3", CultureInfo.InvariantCulture));
        AppendIndented(text, "inboundDelayedFraction", network.InboundDelayedFraction.ToString("F3", CultureInfo.InvariantCulture));
    }

    private static void AppendIssues(StringBuilder text, AlphaStressOutcome outcome)
    {
        text.AppendLine("issues:");
        if (outcome.Issues.Count == 0)
        {
            text.Append(TextIndent);
            text.AppendLine(MissingLabel);
            return;
        }

        foreach (var issue in outcome.Issues)
        {
            text.Append(TextIndent);
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{issue.DiagnosticCode} iteration={issue.IterationIndex?.ToString(CultureInfo.InvariantCulture) ?? MissingLabel} at={issue.ObservedAtUtc:O} message={issue.Message}"));
        }
    }

    private static void AppendRow(StringBuilder text, string label, string value)
    {
        text.Append(label);
        text.Append(RowSeparator);
        text.AppendLine(value);
    }

    private static void AppendIndented(StringBuilder text, string label, string value)
    {
        text.Append(TextIndent);
        text.Append(label);
        text.Append(RowSeparator);
        text.AppendLine(value);
    }
}
