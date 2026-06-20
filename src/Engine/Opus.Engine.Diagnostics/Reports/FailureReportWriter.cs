using System;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Opus.Engine.Diagnostics;
using Opus.Foundation;
using Opus.Foundation.IO;

namespace Opus.Engine.Diagnostics.Reports;

/// <summary>
/// Writes tester failure reports as paired JSON and text artifacts. Writes are
/// lock-protected and atomic per file (write-to-temp then rename) so a torn write cannot
/// leave behind half a report next to a stale earlier one. Exception classification on
/// failure is narrow: only filesystem-shaped exceptions are mapped to
/// <see cref="FailureReportWriteIssue"/>; programming errors and serialization bugs
/// surface as <see cref="FailureReportWriteIssue"/> with the
/// <see cref="DiagnosticCodes.FailureReportWriteFailed"/> code so the caller still gets a
/// structured signal instead of an unhandled exception.
/// </summary>
public sealed class FailureReportWriter
{
    private const string JsonExtension = ".json";
    private const string TextExtension = ".txt";
    private const string DefaultScreenshotExtension = ".png";
    private const string TextIndent = "  ";
    private const string RowSeparator = ": ";
    private const string TextHeader = "Opus failure report";
    private const string MissingScreenshotLabel = "none";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _writeLock = new();
    private readonly FailureReportWriterOptions _options;
    private readonly TimeProvider _clock;

    /// <summary>Creates a writer with explicit filesystem options.</summary>
    public FailureReportWriter(FailureReportWriterOptions options)
        : this(options, TimeProvider.System)
    {
    }

    /// <summary>Test-friendly constructor with an explicit clock for retention sweeps.</summary>
    public FailureReportWriter(FailureReportWriterOptions options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _options = options;
        _clock = clock;
    }

    /// <summary>Writes a report and returns paths or a structured issue.</summary>
    public FailureReportWriteResult Write(FailureReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        try
        {
            _options.Validate();
        }
        catch (ArgumentException ex)
        {
            return FailureReportWriteResult.Failed(BuildIssue(
                DiagnosticCodes.FailureReportConfigurationInvalid,
                ex));
        }

        lock (_writeLock)
        {
            return WriteCore(report);
        }
    }

    private FailureReportWriteResult WriteCore(FailureReport report)
    {
        try
        {
            Directory.CreateDirectory(_options.DirectoryPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            return FailureReportWriteResult.Failed(BuildIssue(
                DiagnosticCodes.FailureReportPermissionDenied,
                ex));
        }
        catch (Exception ex) when (IsFilesystemException(ex))
        {
            return FailureReportWriteResult.Failed(BuildIssue(
                DiagnosticCodes.FailureReportDirectoryUnavailable,
                ex));
        }

        DiagnosticsArtifactRetentionSweeper.Sweep(
            _options.DirectoryPath,
            FailureReportWriterOptions.ArtifactStemPrefix,
            _options.EffectiveRetention,
            _clock.GetUtcNow());

        var stem = BuildFileStem(report);
        var jsonPath = Path.Combine(_options.DirectoryPath, stem + JsonExtension);
        var textPath = Path.Combine(_options.DirectoryPath, stem + TextExtension);

        try
        {
            AtomicFile.WriteAllText(jsonPath, JsonSerializer.Serialize(report, JsonOptions));
            AtomicFile.WriteAllText(textPath, BuildText(report));
        }
        catch (UnauthorizedAccessException ex)
        {
            return FailureReportWriteResult.Failed(BuildIssue(
                DiagnosticCodes.FailureReportPermissionDenied,
                ex));
        }
        catch (JsonException ex)
        {
            return FailureReportWriteResult.Failed(BuildIssue(
                DiagnosticCodes.FailureReportWriteFailed,
                ex));
        }
        catch (Exception ex) when (IsFilesystemException(ex))
        {
            return FailureReportWriteResult.Failed(BuildIssue(
                DiagnosticCodes.FailureReportWriteFailed,
                ex));
        }

        var attachedScreenshot = TryAttachScreenshot(report, stem);
        return FailureReportWriteResult.Success(jsonPath, textPath, attachedScreenshot);
    }

    /// <summary>Copies the report's screenshot next to the freshly written bundle so the
    /// JSON + text + image share one stem and travel together (including through the
    /// retention sweep). Best-effort: a missing source or a filesystem failure leaves the
    /// already-written report intact and returns null rather than failing the whole write —
    /// the visual evidence is supplementary to the structured report.</summary>
    private string? TryAttachScreenshot(FailureReport report, string stem)
    {
        if (report.ScreenshotPath is null || !File.Exists(report.ScreenshotPath))
        {
            return null;
        }

        var extension = Path.GetExtension(report.ScreenshotPath);
        if (string.IsNullOrEmpty(extension))
        {
            extension = DefaultScreenshotExtension;
        }

        var destinationPath = Path.Combine(_options.DirectoryPath, stem + extension);
        try
        {
            AtomicFile.Copy(report.ScreenshotPath, destinationPath);
            return destinationPath;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || IsFilesystemException(ex))
        {
            return null;
        }
    }

    private FailureReportWriteIssue BuildIssue(string code, Exception exception) => new(
        Code: code,
        Severity: LogLevel.Error,
        Path: _options.DirectoryPath,
        Message: string.Create(
            CultureInfo.InvariantCulture,
            $"Failure report write failed for '{_options.DirectoryPath}': {exception.GetType().Name}: {exception.Message}"),
        RemediationHint: "Check that the diagnostics directory exists, is writable, and has free disk space.");

    private static bool IsFilesystemException(Exception exception) =>
        exception is IOException
            or SecurityException
            or NotSupportedException
            or PathTooLongException
            or DirectoryNotFoundException;

    private static string BuildFileStem(FailureReport report) => string.Create(
        CultureInfo.InvariantCulture,
        $"opus-{report.Kind.ToString().ToLowerInvariant()}-{report.CapturedAtUtc:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}");

    private static string BuildText(FailureReport report)
    {
        var text = new StringBuilder();
        text.AppendLine(TextHeader);
        AppendRow(text, "kind", report.Kind.ToString());
        AppendRow(text, "capturedAtUtc", report.CapturedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        text.AppendLine(report.Build.ToReportText());
        AppendRow(text, "adapter", report.Adapter.AdapterName);
        AppendRow(text, "resolution", string.Create(
            CultureInfo.InvariantCulture,
            $"{report.Adapter.BackBufferWidth}x{report.Adapter.BackBufferHeight}"));
        AppendAdapterHardware(text, report.Adapter.Hardware);
        AppendRow(text, "screenshot", report.ScreenshotPath ?? MissingScreenshotLabel);
        AppendNetwork(text, report);
        AppendConsumerLines(text, report);
        AppendExceptions(text, report);
        AppendLogTail(text, report);
        return text.ToString();
    }

    private static void AppendNetwork(StringBuilder text, FailureReport report)
    {
        text.AppendLine("network:");
        if (report.Network is null)
        {
            text.Append(TextIndent);
            text.AppendLine(MissingScreenshotLabel);
            return;
        }

        var network = report.Network;
        AppendIndented(text, "session", network.DisplayName);
        AppendIndented(text, "role", network.Role);
        AppendIndented(text, "state", network.State);
        AppendIndented(text, "connectedPeers", network.ConnectedPeerCount.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "packetsIn", network.PacketsReceived.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "packetsOut", network.PacketsSent.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "packetsSendDropped", network.PacketsSendDropped.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "bytesIn", network.BytesReceived.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "bytesOut", network.BytesSent.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "reconnectAttempts", network.ReconnectAttempts.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "queuedDropped", network.QueuedPayloadsDropped.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "rejectedConnections", network.RejectedConnections.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "droppedInbound", network.DroppedInboundPayloads.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "rateLimited", network.RateLimitedInboundPayloads.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "rttSamples", network.RttSampleCount.ToString(CultureInfo.InvariantCulture));
        AppendIndented(text, "rttMeanMs", network.RttMean.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture));
        AppendIndented(text, "rttP95Ms", network.RttP95.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture));
        AppendIndented(text, "rateWindowMs", network.RateWindow.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture));
        AppendIndented(text, "packetsInPerSec", network.PacketsReceivedPerSecond.ToString("F2", CultureInfo.InvariantCulture));
        AppendIndented(text, "packetsOutPerSec", network.PacketsSentPerSecond.ToString("F2", CultureInfo.InvariantCulture));
        AppendIndented(text, "bytesInPerSec", network.BytesReceivedPerSecond.ToString("F2", CultureInfo.InvariantCulture));
        AppendIndented(text, "bytesOutPerSec", network.BytesSentPerSecond.ToString("F2", CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(network.LastFaultCode))
        {
            AppendIndented(text, "lastFaultCode", network.LastFaultCode!);
        }

        if (!string.IsNullOrWhiteSpace(network.LastFaultDetail))
        {
            AppendIndented(text, "lastFaultDetail", network.LastFaultDetail!);
        }
    }

    private static void AppendIndented(StringBuilder text, string label, string value)
    {
        text.Append(TextIndent);
        text.Append(label);
        text.Append(RowSeparator);
        text.AppendLine(value);
    }

    private static void AppendRow(StringBuilder text, string label, string value)
    {
        text.Append(label);
        text.Append(RowSeparator);
        text.AppendLine(value);
    }

    private static void AppendAdapterHardware(StringBuilder text, DiagnosticAdapterHardware hardware)
    {
        AppendRow(text, "adapterVendor", hardware.VendorName);
        AppendRow(text, "adapterVendorId", string.Create(CultureInfo.InvariantCulture, $"0x{hardware.VendorId:X4}"));
        AppendRow(text, "adapterDeviceId", string.Create(CultureInfo.InvariantCulture, $"0x{hardware.DeviceId:X4}"));
        AppendRow(text, "adapterClass", hardware.Class.ToString().ToLowerInvariant());
        AppendRow(text, "adapterVramMb", hardware.DedicatedVideoMemoryMegabytes.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendConsumerLines(StringBuilder text, FailureReport report)
    {
        text.AppendLine("consumer:");
        if (report.ConsumerLines.Count == 0)
        {
            text.Append(TextIndent);
            text.AppendLine(MissingScreenshotLabel);
            return;
        }

        foreach (var line in report.ConsumerLines)
        {
            text.Append(TextIndent);
            text.AppendLine(line);
        }
    }

    private static void AppendExceptions(StringBuilder text, FailureReport report)
    {
        text.AppendLine("exceptions:");
        if (report.ExceptionChain.Count == 0)
        {
            text.Append(TextIndent);
            text.AppendLine(MissingScreenshotLabel);
            return;
        }

        foreach (var exception in report.ExceptionChain)
        {
            text.Append(TextIndent);
            text.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{exception.Type}: {exception.Message}"));
        }
    }

    private static void AppendLogTail(StringBuilder text, FailureReport report)
    {
        text.AppendLine("lastLogLines:");
        if (report.LastLogLines.Count == 0)
        {
            text.Append(TextIndent);
            text.AppendLine(MissingScreenshotLabel);
            return;
        }

        foreach (var line in report.LastLogLines)
        {
            text.Append(TextIndent);
            text.AppendLine(line);
        }
    }
}
