using System;
using System.Collections.Generic;
using System.IO;
using Opus.Foundation;

namespace Opus.Localisation;

/// <summary>
/// In-memory <see cref="ITranslationCatalog"/> built from a key,value CSV. Hosts load
/// the bytes through their own VFS and pass a Stream — this class is host-agnostic.
///
/// Format: first line is a header (<c>key,text</c>), discarded. Each remaining line is
/// split on the FIRST comma — keys never contain commas (they're dotted identifiers),
/// values may contain anything except newlines. Embedded commas in values do NOT need
/// escaping under this rule. Lines starting with <c>#</c> or empty are skipped.
/// </summary>
public sealed class CsvCatalog : ITranslationCatalog
{
    private readonly Dictionary<string, string> _entries;

    public CsvCatalog(string locale, IReadOnlyDictionary<string, string> entries)
    {
        Locale = Ensure.NotNullOrEmpty(locale);
        _entries = new Dictionary<string, string>(entries, StringComparer.Ordinal);
    }

    public string Locale { get; }

    public IReadOnlyCollection<string> AllKeys => _entries.Keys;

    public string Get(TranslationKey key) =>
        _entries.TryGetValue(key.Key, out var v) ? v : key.Key;

    public Result<string> TryGet(TranslationKey key) =>
        _entries.TryGetValue(key.Key, out var v)
            ? Result<string>.Ok(v)
            : Result<string>.Err(new Error(ErrorCode.TranslationKeyMissing, $"No translation for '{key.Key}' in '{Locale}'."));

    public bool Has(TranslationKey key) => _entries.ContainsKey(key.Key);

    public static CsvCatalog ReadFrom(string locale, Stream stream)
    {
        Ensure.NotNullOrEmpty(locale);
        Ensure.NotNull(stream);

        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        using var reader = new StreamReader(stream);
        var lineNo = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNo++;
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            // Skip header — first non-comment line whose first token is literally "key".
            var split = line.IndexOf(',', StringComparison.Ordinal);
            if (split < 0)
            {
                continue;
            }

            var key = line.Substring(0, split);
            var value = line.Substring(split + 1);

            if (lineNo == 1 && string.Equals(key, "key", StringComparison.Ordinal))
            {
                continue;
            }

            // Late header (e.g. CRLF / BOM weirdness): skip if key already seen + literal "key".
            if (string.Equals(key, "key", StringComparison.Ordinal) && string.Equals(value, "text", StringComparison.Ordinal))
            {
                continue;
            }

            dict[key] = value;
        }

        return new CsvCatalog(locale, dict);
    }
}
