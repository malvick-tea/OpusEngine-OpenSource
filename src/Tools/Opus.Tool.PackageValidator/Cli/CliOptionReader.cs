namespace Opus.Tool.PackageValidator;

/// <summary>
/// Shared <c>--option value</c> reader for the package tool subcommands. Recognises the
/// option by name, advances the argument cursor onto the value, and reports a missing or
/// option-shaped value to <paramref name="stderr"/>. A recognised option with no usable
/// value returns <c>true</c> with a null <paramref name="value"/> so the caller can stop
/// parsing with an argument error.
/// </summary>
internal static class CliOptionReader
{
    public static bool TryReadOption(
        string current,
        string optionName,
        string[] args,
        ref int index,
        TextWriter stderr,
        out string? value)
    {
        value = null;
        if (!string.Equals(current, optionName, StringComparison.Ordinal))
        {
            return false;
        }

        if (index + 1 >= args.Length)
        {
            stderr.WriteLine($"Missing value for {optionName}.");
            // Returning true with value == null signals "option recognised but value missing".
            return true;
        }

        value = args[++index];
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal))
        {
            stderr.WriteLine($"Missing value for {optionName}.");
            value = null;
        }

        return true;
    }
}
