namespace Opus.Tool.PackageValidator;

/// <summary>Stable process exit codes for the package validator command.</summary>
public static class PackageValidatorExitCodes
{
    /// <summary>Validation completed and the package is valid.</summary>
    public const int Success = 0;

    /// <summary>Validation completed and found package errors.</summary>
    public const int ValidationFailed = 1;

    /// <summary>CLI arguments are invalid.</summary>
    public const int InvalidArguments = 2;

    /// <summary>The tool hit an unexpected IO or runtime failure.</summary>
    public const int ToolFailure = 3;
}
