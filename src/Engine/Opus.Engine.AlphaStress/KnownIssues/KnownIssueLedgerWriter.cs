using System;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Opus.Foundation;

namespace Opus.Engine.AlphaStress.KnownIssues;

/// <summary>
/// Writes a <see cref="KnownIssueLedger"/> as deterministic indented JSON at the
/// configured path. Atomic per file (write-to-temp then replace), narrow filesystem
/// exception classification, never throws past the public API. Mirrors the M7 +
/// M9 atomic writers so reporter pipelines treat ledger persistence the same way they
/// treat failure reports and smoke evidence.
/// </summary>
public sealed class KnownIssueLedgerWriter
{
    private const string TempExtension = ".tmp";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _writeLock = new();
    private readonly KnownIssueLedgerWriterOptions _options;

    /// <summary>Creates a writer with explicit filesystem options.</summary>
    public KnownIssueLedgerWriter(KnownIssueLedgerWriterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>Persists the ledger. Returns a structured success or failure outcome.</summary>
    public KnownIssueLedgerWriteResult Write(KnownIssueLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        try
        {
            _options.Validate();
        }
        catch (ArgumentException ex)
        {
            return KnownIssueLedgerWriteResult.Failed(BuildIssue(
                ex,
                "Configure a non-empty file path before invoking the known-issue writer."));
        }

        lock (_writeLock)
        {
            return WriteCore(ledger);
        }
    }

    private KnownIssueLedgerWriteResult WriteCore(KnownIssueLedger ledger)
    {
        try
        {
            var directory = Path.GetDirectoryName(_options.FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            return KnownIssueLedgerWriteResult.Failed(BuildIssue(
                ex,
                "Grant write permission to the known-issue ledger directory."));
        }
        catch (Exception ex) when (IsFilesystemException(ex))
        {
            return KnownIssueLedgerWriteResult.Failed(BuildIssue(
                ex,
                "Free disk space or fix path validity before re-running the stress harness."));
        }

        try
        {
            var json = JsonSerializer.Serialize(ledger.Records, JsonOptions);
            WriteAtomic(_options.FilePath, json);
        }
        catch (UnauthorizedAccessException ex)
        {
            return KnownIssueLedgerWriteResult.Failed(BuildIssue(
                ex,
                "Grant write permission to the known-issue ledger file."));
        }
        catch (JsonException ex)
        {
            return KnownIssueLedgerWriteResult.Failed(BuildIssue(
                ex,
                "Known-issue ledger failed to serialise. Capture stack trace and report to engine lead."));
        }
        catch (Exception ex) when (IsFilesystemException(ex))
        {
            return KnownIssueLedgerWriteResult.Failed(BuildIssue(
                ex,
                "Known-issue ledger write failed. Free disk space or relocate the diagnostics root."));
        }

        return KnownIssueLedgerWriteResult.Success(_options.FilePath);
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

    private KnownIssueLedgerWriteIssue BuildIssue(Exception exception, string remediation) => new(
        Code: AlphaStressDiagnosticCodes.KnownIssueLedgerWriteFailed,
        Severity: LogLevel.Error,
        Path: _options.FilePath,
        Message: string.Create(
            CultureInfo.InvariantCulture,
            $"Known-issue ledger write failed for '{_options.FilePath}': {exception.GetType().Name}: {exception.Message}"),
        RemediationHint: remediation);

    private static bool IsFilesystemException(Exception exception) =>
        exception is IOException
            or SecurityException
            or NotSupportedException
            or PathTooLongException
            or DirectoryNotFoundException;
}
