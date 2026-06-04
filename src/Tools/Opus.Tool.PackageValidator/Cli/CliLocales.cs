namespace Opus.Tool.PackageValidator;

/// <summary>Shared locale vocabulary for the package tool subcommands.</summary>
internal static class CliLocales
{
    /// <summary>Default locale when none is requested.</summary>
    public const string Default = "en";

    /// <summary>Human-readable list of supported locales for usage messages.</summary>
    public const string SupportedList = "en|ru";

    /// <summary>True when <paramref name="locale"/> is a supported diagnostic catalog locale.</summary>
    public static bool IsSupported(string locale) =>
        string.Equals(locale, "en", StringComparison.Ordinal)
        || string.Equals(locale, "ru", StringComparison.Ordinal);
}
