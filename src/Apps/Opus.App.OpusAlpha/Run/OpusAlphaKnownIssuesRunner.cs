using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Opus.App.OpusAlpha.Cli;
using Opus.Engine.AlphaStress.KnownIssues;
using Opus.Foundation;

namespace Opus.App.OpusAlpha.Run;

/// <summary>
/// Runs the known-issue ledger merge and diff sub-commands. Both modes are filesystem-
/// boundary surfaces: failures map to deterministic exit codes (missing argument,
/// malformed input, write failure) so tester pipelines can shell out without
/// downstream exception handling.
/// </summary>
public static class OpusAlphaKnownIssuesRunner
{
    private const int ExitClean = 0;
    private const int ExitMissingInput = 1;
    private const int ExitLoadFailed = 2;
    private const int ExitWriteFailed = 3;
    private const int ExitDifferent = 4;

    private const string TempExtension = ".tmp";
    private const string MergeHeader = "Opus known-issue ledger merge";
    private const string DiffHeader = "Opus known-issue ledger diff";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Merges two ledgers and writes the result to the output path.</summary>
    public static int RunMerge(OpusAlphaArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        if (string.IsNullOrWhiteSpace(args.KnownIssuesBasePath))
        {
            log.Error("known-issues-merge requires --base <path>.");
            return ExitMissingInput;
        }

        if (string.IsNullOrWhiteSpace(args.KnownIssuesOverlayPath))
        {
            log.Error("known-issues-merge requires --overlay <path>.");
            return ExitMissingInput;
        }

        if (string.IsNullOrWhiteSpace(args.KnownIssuesOutputPath))
        {
            log.Error("known-issues-merge requires --output <path>.");
            return ExitMissingInput;
        }

        if (!TryLoadLedger(args.KnownIssuesBasePath!, log, out var baseLedger))
        {
            return ExitLoadFailed;
        }

        if (!TryLoadLedger(args.KnownIssuesOverlayPath!, log, out var overlayLedger))
        {
            return ExitLoadFailed;
        }

        var merged = KnownIssueLedgerMerger.Merge(baseLedger, overlayLedger);
        if (!TryWriteLedger(args.KnownIssuesOutputPath!, merged, log))
        {
            return ExitWriteFailed;
        }

        log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"{MergeHeader}: {merged.TotalCount} record(s) written to '{args.KnownIssuesOutputPath}'."));
        return ExitClean;
    }

    /// <summary>Diffs two ledgers and prints (or persists) the structured change list.</summary>
    public static int RunDiff(OpusAlphaArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        if (string.IsNullOrWhiteSpace(args.KnownIssuesLeftPath))
        {
            log.Error("known-issues-diff requires --left <path>.");
            return ExitMissingInput;
        }

        if (string.IsNullOrWhiteSpace(args.KnownIssuesRightPath))
        {
            log.Error("known-issues-diff requires --right <path>.");
            return ExitMissingInput;
        }

        if (!TryLoadLedger(args.KnownIssuesLeftPath!, log, out var left))
        {
            return ExitLoadFailed;
        }

        if (!TryLoadLedger(args.KnownIssuesRightPath!, log, out var right))
        {
            return ExitLoadFailed;
        }

        var diff = KnownIssueLedgerDiff.Compute(left, right);
        var rendered = args.KnownIssuesDiffFormat switch
        {
            KnownIssuesDiffFormat.Json => RenderJson(diff),
            _ => RenderText(diff),
        };
        if (!string.IsNullOrWhiteSpace(args.KnownIssuesOutputPath))
        {
            if (!TryWriteText(args.KnownIssuesOutputPath!, rendered, log))
            {
                return ExitWriteFailed;
            }

            log.Info(string.Create(
                CultureInfo.InvariantCulture,
                $"{DiffHeader}: persisted to '{args.KnownIssuesOutputPath}'."));
        }
        else
        {
            Console.Out.Write(rendered);
        }

        return diff.HasChanges ? ExitDifferent : ExitClean;
    }

    private static bool TryLoadLedger(string path, ILog log, out KnownIssueLedger ledger)
    {
        ledger = KnownIssueLedger.Empty;
        if (!File.Exists(path))
        {
            log.Error(string.Create(CultureInfo.InvariantCulture, $"Known-issue ledger '{path}' not found."));
            return false;
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            log.Error(string.Create(CultureInfo.InvariantCulture, $"Known-issue ledger '{path}' could not be read: {ex.Message}"));
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            log.Error(string.Create(CultureInfo.InvariantCulture, $"Known-issue ledger '{path}' could not be read: {ex.Message}"));
            return false;
        }

        try
        {
            var records = JsonSerializer.Deserialize<KnownIssueRecord[]>(json, ReadOptions);
            if (records is null)
            {
                log.Error(string.Create(CultureInfo.InvariantCulture, $"Known-issue ledger '{path}' deserialised to null."));
                return false;
            }

            ledger = KnownIssueLedger.Create(records);
            return true;
        }
        catch (JsonException ex)
        {
            log.Error(string.Create(CultureInfo.InvariantCulture, $"Known-issue ledger '{path}' failed to parse: {ex.Message}"));
            return false;
        }
        catch (ArgumentException ex)
        {
            log.Error(string.Create(CultureInfo.InvariantCulture, $"Known-issue ledger '{path}' failed validation: {ex.Message}"));
            return false;
        }
    }

    private static bool TryWriteLedger(string path, KnownIssueLedger ledger, ILog log)
    {
        var json = JsonSerializer.Serialize(ledger.Records, WriteOptions);
        return TryWriteText(path, json, log);
    }

    private static bool TryWriteText(string path, string content, ILog log)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            log.Error(string.Create(CultureInfo.InvariantCulture, $"Failed to create directory for '{path}': {ex.Message}"));
            return false;
        }
        catch (IOException ex)
        {
            log.Error(string.Create(CultureInfo.InvariantCulture, $"Failed to create directory for '{path}': {ex.Message}"));
            return false;
        }

        var tempPath = path + TempExtension;
        try
        {
            File.WriteAllText(tempPath, content, Encoding.UTF8);
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, path);
            }

            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            log.Error(string.Create(CultureInfo.InvariantCulture, $"Failed to write '{path}': {ex.Message}"));
            return false;
        }
        catch (IOException ex)
        {
            log.Error(string.Create(CultureInfo.InvariantCulture, $"Failed to write '{path}': {ex.Message}"));
            return false;
        }
    }

    /// <summary>Renders the diff as a grep-friendly multi-line text block. Exposed
    /// internally so tests can assert on the exact output shape.</summary>
    internal static string RenderText(KnownIssueLedgerDiff diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        var text = new StringBuilder();
        text.AppendLine(DiffHeader);
        text.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"added: {diff.AddedCount} removed: {diff.RemovedCount} changed: {diff.ChangedCount} unchanged: {diff.UnchangedCount}"));
        foreach (var change in diff.Changes)
        {
            switch (change.Kind)
            {
                case KnownIssueChangeKind.Added:
                    text.AppendLine(string.Create(
                        CultureInfo.InvariantCulture,
                        $"+ {change.Id} severity={change.Right!.Severity} status={change.Right!.Status} summary={change.Right!.Summary}"));
                    break;
                case KnownIssueChangeKind.Removed:
                    text.AppendLine(string.Create(
                        CultureInfo.InvariantCulture,
                        $"- {change.Id} severity={change.Left!.Severity} status={change.Left!.Status} summary={change.Left!.Summary}"));
                    break;
                case KnownIssueChangeKind.Changed:
                    text.AppendLine(string.Create(
                        CultureInfo.InvariantCulture,
                        $"~ {change.Id} severity={change.Left!.Severity}->{change.Right!.Severity} status={change.Left!.Status}->{change.Right!.Status} summary={change.Right!.Summary}"));
                    break;
                case KnownIssueChangeKind.Unchanged:
                    text.AppendLine(string.Create(
                        CultureInfo.InvariantCulture,
                        $"= {change.Id} severity={change.Right!.Severity} status={change.Right!.Status}"));
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unhandled known-issue change kind '{change.Kind}'.");
            }
        }

        return text.ToString();
    }

    /// <summary>Renders the diff as structured JSON. Exposed internally for tests.</summary>
    internal static string RenderJson(KnownIssueLedgerDiff diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        var payload = new DiffPayload(
            Added: diff.AddedCount,
            Removed: diff.RemovedCount,
            Changed: diff.ChangedCount,
            Unchanged: diff.UnchangedCount,
            Changes: diff.Changes);
        return JsonSerializer.Serialize(payload, WriteOptions);
    }

    private sealed record DiffPayload(
        int Added,
        int Removed,
        int Changed,
        int Unchanged,
        System.Collections.Generic.IReadOnlyList<KnownIssueLedgerChange> Changes);
}
