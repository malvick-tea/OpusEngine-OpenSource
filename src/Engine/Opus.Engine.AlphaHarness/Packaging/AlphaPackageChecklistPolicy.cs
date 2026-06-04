using System;
using System.Collections.Generic;
using System.Linq;
using Opus.Content.Packaging.Manifest;
using Opus.Foundation;

namespace Opus.Engine.AlphaHarness.Packaging;

/// <summary>
/// Data shape describing what an Opus 0.1 alpha package must contain. The checklist reads
/// the policy once and never mutates it; consumers that need a tighter or looser policy
/// build a new instance instead of toggling flags at runtime.
/// </summary>
/// <param name="EngineProductName">Expected value of <c>engine.product</c> in the manifest;
/// defaults to <see cref="EngineIdentity"/>'s <c>ProductName</c>.</param>
/// <param name="RequiredFeatures">Feature identifiers (matched against
/// <c>PackageFeatures</c>) that the manifest must list in <c>requiredFeatures</c>.</param>
/// <param name="RequiredAssetKinds">Asset families that must each have at least one
/// matching <c>files[]</c> entry in the manifest.</param>
/// <param name="RequiredLocales">Localisation locales whose file must appear as the
/// basename (case-insensitive) of at least one entry typed as
/// <c>localisation.json</c> or <c>localisation.csv</c>.</param>
public sealed record AlphaPackageChecklistPolicy(
    string EngineProductName,
    IReadOnlyList<string> RequiredFeatures,
    IReadOnlyList<AlphaPackageRequiredAssetKind> RequiredAssetKinds,
    IReadOnlyList<string> RequiredLocales)
{
    /// <summary>Canonical Opus 0.1 alpha checklist: models, textures, fonts, localisation,
    /// with EN and RU locales mandatory. Driven from the same engine identity surface as
    /// the build banner so the policy moves with the product.</summary>
    public static AlphaPackageChecklistPolicy Default { get; } = new(
        EngineProductName: EngineIdentity.Current.ProductName,
        RequiredFeatures: new[]
        {
            PackageFeatures.Models,
            PackageFeatures.Textures,
            PackageFeatures.Fonts,
            PackageFeatures.Localisation,
        },
        RequiredAssetKinds: new[]
        {
            AlphaPackageRequiredAssetKind.Model,
            AlphaPackageRequiredAssetKind.Texture,
            AlphaPackageRequiredAssetKind.Font,
            AlphaPackageRequiredAssetKind.Localisation,
        },
        RequiredLocales: new[] { "en", "ru" });

    /// <summary>Throws when the policy is internally inconsistent. Validation is loud so a
    /// host that wires a custom policy fails at the boundary instead of producing silently
    /// empty checklists.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EngineProductName))
        {
            throw new ArgumentException("EngineProductName must not be empty.", nameof(EngineProductName));
        }

        ArgumentNullException.ThrowIfNull(RequiredFeatures);
        ArgumentNullException.ThrowIfNull(RequiredAssetKinds);
        ArgumentNullException.ThrowIfNull(RequiredLocales);
        if (RequiredFeatures.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("RequiredFeatures must not contain empty entries.", nameof(RequiredFeatures));
        }

        if (RequiredLocales.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("RequiredLocales must not contain empty entries.", nameof(RequiredLocales));
        }
    }
}
