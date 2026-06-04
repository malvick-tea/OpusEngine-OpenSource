namespace Opus.Content.Packaging.Validation;

/// <summary>Helper that builds the immutable argument dictionary attached to a diagnostic.
/// Rejects duplicate keys instead of silently last-write-wins so a caller-side typo does
/// not quietly hide an argument from the localizer.</summary>
internal static class PackageDiagnosticArguments
{
    public static IReadOnlyDictionary<string, string> Create(params (string Key, string Value)[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var result = new Dictionary<string, string>(values.Length, StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(value);
            if (!result.TryAdd(key, value))
            {
                throw new ArgumentException(
                    $"Duplicate diagnostic argument key '{key}'.",
                    nameof(values));
            }
        }

        return result;
    }
}
