using System;
using System.Collections.Generic;
using Opus.Content.Packaging.Manifest;
using Opus.Content.Packaging.Validation;

namespace Opus.Content.Packaging.Generation;

/// <summary>
/// Inputs for <see cref="PackageManifestGenerator"/>. The package identity is caller-supplied
/// (the generator cannot invent a package id or display name); the engine target, file list,
/// sizes, and SHA-256 hashes are computed from the content tree under <see cref="PackageRoot"/>.
/// </summary>
/// <param name="PackageRoot">Directory whose files become the generated manifest's file list.</param>
/// <param name="Package">Caller-authored package identity block copied into the manifest.</param>
public sealed record PackageGenerationRequest(
    string PackageRoot,
    ContentPackageInfo Package)
{
    /// <summary>Manifest file name skipped during the content walk (so regenerating in place
    /// does not list the manifest as one of its own files). Defaults to the validator's
    /// standard name.</summary>
    public string ManifestFileName { get; init; } = PackageValidator.DefaultManifestFileName;

    /// <summary>Optional authoring/provenance block copied verbatim into the manifest.</summary>
    public ContentPackageAuthoring? Authoring { get; init; }

    /// <summary>Optional entrypoints block copied verbatim into the manifest.</summary>
    public ContentPackageEntrypoints? Entrypoints { get; init; }

    /// <summary>Required-feature identifiers copied into the manifest. Defaults to empty.</summary>
    public IReadOnlyList<string> RequiredFeatures { get; init; } = Array.Empty<string>();

    /// <summary>Per-extension asset-type overrides (keyed by lower-case extension including
    /// the leading dot, e.g. <c>.dat</c>) consulted before the built-in extension inference.
    /// Lets an author classify a file the built-in map cannot. Defaults to empty.</summary>
    public IReadOnlyDictionary<string, string> TypeOverrides { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
