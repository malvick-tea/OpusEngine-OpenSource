namespace Opus.Content.Packaging.Manifest;

/// <summary>
/// Version of the package manifest schema. Major changes are breaking; minor changes are
/// additive and must be ignored by older validators unless a required feature says otherwise.
/// </summary>
public sealed record ManifestFormatVersion(int Major, int Minor);
