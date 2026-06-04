namespace Opus.Content.Packaging.Validation;

/// <summary>Validation policy for files present in the package directory but absent from the manifest.</summary>
public enum PackageUnlistedFilePolicy
{
    /// <summary>Report unlisted files as warnings.</summary>
    Warning,

    /// <summary>Report unlisted files as errors.</summary>
    Error,

    /// <summary>Ignore unlisted files.</summary>
    Ignore,
}
