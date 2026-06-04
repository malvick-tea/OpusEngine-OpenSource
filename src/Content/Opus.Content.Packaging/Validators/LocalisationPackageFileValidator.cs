using System.Text;
using System.Text.Json;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Manifest;
using Opus.Content.Packaging.Validation;

namespace Opus.Content.Packaging.Validators;

/// <summary>Headless localisation validator. Accepts JSON objects keyed by translation
/// identifier and CSV files with a <c>key,text</c> header. Returns a sorted key set the
/// outer validator can use for cross-locale parity diagnostics.</summary>
internal sealed class LocalisationPackageFileValidator : IPackageFileValidator
{
    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

    public bool CanValidate(ContentPackageFile file) =>
        string.Equals(file.Type, PackageAssetTypes.LocalisationJson, StringComparison.Ordinal)
        || string.Equals(file.Type, PackageAssetTypes.LocalisationCsv, StringComparison.Ordinal);

    public IReadOnlyList<PackageDiagnostic> Validate(PackageValidationContext context) =>
        TryReadKeySet(context, out _)
            ? Array.Empty<PackageDiagnostic>()
            : LocalisationError(context, "Localisation file could not be parsed or has no keys.");

    /// <summary>Parses the localisation file and returns its translation keys, sorted
    /// ordinally so cross-locale parity comparisons are deterministic regardless of file
    /// order on disk. Returns <c>false</c> when the file cannot be parsed or carries no
    /// usable keys.</summary>
    public static bool TryReadKeySet(PackageValidationContext context, out IReadOnlySet<string> keys)
    {
        if (string.Equals(context.File.Type, PackageAssetTypes.LocalisationJson, StringComparison.Ordinal))
        {
            return TryReadJsonKeys(context.Bytes, out keys);
        }

        if (string.Equals(context.File.Type, PackageAssetTypes.LocalisationCsv, StringComparison.Ordinal))
        {
            keys = ReadCsvKeys(context.Bytes);
            return keys.Count > 0;
        }

        keys = new SortedSet<string>(StringComparer.Ordinal);
        return false;
    }

    private static bool TryReadJsonKeys(byte[] bytes, out IReadOnlySet<string> keys)
    {
        try
        {
            var json = StripBom(bytes);
            var entries = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (entries is null)
            {
                keys = new SortedSet<string>(StringComparer.Ordinal);
                return false;
            }

            var sorted = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.Key))
                {
                    sorted.Add(entry.Key);
                }
            }

            keys = sorted;
            return sorted.Count > 0;
        }
        catch (JsonException)
        {
            keys = new SortedSet<string>(StringComparer.Ordinal);
            return false;
        }
    }

    private static IReadOnlySet<string> ReadCsvKeys(byte[] bytes)
    {
        var sorted = new SortedSet<string>(StringComparer.Ordinal);
        var text = Encoding.UTF8.GetString(StripBom(bytes));
        using var reader = new StringReader(text);
        string? line;
        var seenHeader = false;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var split = line.IndexOf(',', StringComparison.Ordinal);
            if (split <= 0)
            {
                continue;
            }

            var key = line[..split];
            if (!seenHeader && string.Equals(key, "key", StringComparison.Ordinal))
            {
                seenHeader = true;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(key))
            {
                sorted.Add(key);
            }
        }

        return sorted;
    }

    private static byte[] StripBom(byte[] bytes)
    {
        if (bytes.Length < Utf8Bom.Length || !bytes.AsSpan(0, Utf8Bom.Length).SequenceEqual(Utf8Bom))
        {
            return bytes;
        }

        var trimmed = new byte[bytes.Length - Utf8Bom.Length];
        Array.Copy(bytes, Utf8Bom.Length, trimmed, 0, trimmed.Length);
        return trimmed;
    }

    private static IReadOnlyList<PackageDiagnostic> LocalisationError(PackageValidationContext context, string reason) =>
        new[]
        {
            PackageDiagnosticBuilder.FileError(
                PackageDiagnosticCode.LocalisationInvalid,
                context.RelativePath,
                $"Localisation file '{context.RelativePath.Value}' is invalid: {reason}",
                "Provide a JSON object or key,text CSV with at least one key.",
                "package.localisation.invalid",
                PackageDiagnosticArguments.Create(
                    ("path", context.RelativePath.Value),
                    ("reason", reason))),
        };
}
