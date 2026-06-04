using System.Text.Json;
using Opus.Foundation;

namespace Opus.Content.Packaging.Manifest;

/// <summary>
/// Reads and writes package manifests with the canonical M6 JSON options. The reader
/// normalises optional collection fields (<see cref="ContentPackageManifest.Files"/>,
/// <see cref="ContentPackageManifest.RequiredFeatures"/>,
/// <see cref="ContentPackageEntrypoints.Locales"/>) to empty arrays when the JSON omits
/// them, so downstream validators can iterate without nullable-as-sentinel checks.
/// </summary>
public static class ContentPackageManifestReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Parses a manifest stream into the immutable manifest model. Returns a structured
    /// <see cref="Result{T}"/> instead of throwing on malformed JSON so the validator can
    /// emit a single diagnostic rather than crashing through a corrupt-package boot path.
    /// </summary>
    public static Result<ContentPackageManifest> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            var manifest = JsonSerializer.Deserialize<ContentPackageManifest>(stream, JsonOptions);
            if (manifest is null)
            {
                return Result<ContentPackageManifest>.Err(
                    ErrorCode.DataSchemaMismatch, "Package manifest JSON is empty.");
            }

            return Result<ContentPackageManifest>.Ok(Normalise(manifest));
        }
        catch (JsonException ex)
        {
            return Result<ContentPackageManifest>.Err(new Error(
                ErrorCode.DataSchemaMismatch,
                "Package manifest JSON is malformed.",
                ex));
        }
    }

    /// <summary>
    /// Serialises a manifest using the same property names consumed by <see cref="Read"/>.
    /// </summary>
    public static string Write(ContentPackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return JsonSerializer.Serialize(manifest, JsonOptions);
    }

    private static ContentPackageManifest Normalise(ContentPackageManifest manifest)
    {
        var normalised = manifest with
        {
            Files = manifest.Files ?? Array.Empty<ContentPackageFile>(),
            RequiredFeatures = manifest.RequiredFeatures ?? Array.Empty<string>(),
        };

        if (normalised.Entrypoints is { Locales: null } entrypoints)
        {
            normalised = normalised with
            {
                Entrypoints = entrypoints with { Locales = Array.Empty<string>() },
            };
        }

        return normalised;
    }
}
