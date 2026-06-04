namespace Opus.Tool.PackageValidator;

/// <summary>Parsed options for the package tool's <c>generate</c> subcommand.</summary>
internal sealed record PackageGenerateOptions(
    string ContentRoot,
    string Id,
    string DisplayName,
    string Version,
    string? CreatedAtUtc,
    string OutputPath,
    string Locale);
