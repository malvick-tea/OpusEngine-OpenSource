using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Opus.Engine.AlphaStress.KnownIssues;
using Opus.Foundation;

namespace Opus.App.OpusAlpha.Run;

/// <summary>
/// Loads a <see cref="KnownIssueLedger"/> from a JSON path supplied to the M11 stress
/// runner. The loader is boundary-style: never throws past the public surface, logs the
/// failure and returns <see cref="KnownIssueLedger.Empty"/> when the file is missing or
/// malformed so the stress run continues with no ledger evaluation.
/// </summary>
public static class OpusAlphaKnownIssuesLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Loads a ledger from <paramref name="path"/>. Returns
    /// <see cref="KnownIssueLedger.Empty"/> when the path is null/empty, the file is
    /// missing, the JSON is malformed, or the ledger validation rejects the payload.</summary>
    public static KnownIssueLedger Load(string? path, ILog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (string.IsNullOrWhiteSpace(path))
        {
            return KnownIssueLedger.Empty;
        }

        if (!File.Exists(path))
        {
            log.Warn($"Known-issue ledger '{path}' was not found; continuing with an empty ledger.");
            return KnownIssueLedger.Empty;
        }

        try
        {
            var json = File.ReadAllText(path);
            var records = JsonSerializer.Deserialize<List<KnownIssueRecord>>(json, JsonOptions);
            if (records is null)
            {
                log.Warn($"Known-issue ledger '{path}' deserialised to null; continuing with an empty ledger.");
                return KnownIssueLedger.Empty;
            }

            return KnownIssueLedger.Create(records);
        }
        catch (JsonException ex)
        {
            log.Warn($"Known-issue ledger '{path}' failed to parse: {ex.Message}");
            return KnownIssueLedger.Empty;
        }
        catch (ArgumentException ex)
        {
            log.Warn($"Known-issue ledger '{path}' failed validation: {ex.Message}");
            return KnownIssueLedger.Empty;
        }
        catch (IOException ex)
        {
            log.Warn($"Known-issue ledger '{path}' could not be read: {ex.Message}");
            return KnownIssueLedger.Empty;
        }
    }
}
