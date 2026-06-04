using System.Text.Json;
using Opus.Foundation;

namespace Opus.Content.Packaging.Signing;

/// <summary>
/// Reads and writes the <c>opus.package.sig</c> signature envelope using the same canonical
/// JSON options as the manifest reader, so a signature serialises predictably.
/// </summary>
public static class PackageSignatureReader
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
    /// Parses a signature envelope from a stream. Returns a structured <see cref="Result{T}"/>
    /// instead of throwing so the verifier can emit a single diagnostic rather than crashing on
    /// a corrupt envelope.
    /// </summary>
    public static Result<PackageSignature> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            var signature = JsonSerializer.Deserialize<PackageSignature>(stream, JsonOptions);
            if (signature is null)
            {
                return Result<PackageSignature>.Err(
                    ErrorCode.DataSchemaMismatch, "Package signature JSON is empty.");
            }

            return Result<PackageSignature>.Ok(signature);
        }
        catch (JsonException ex)
        {
            return Result<PackageSignature>.Err(new Error(
                ErrorCode.DataSchemaMismatch,
                "Package signature JSON is malformed.",
                ex));
        }
    }

    /// <summary>Serialises a signature envelope with the canonical options.</summary>
    public static string Write(PackageSignature signature)
    {
        ArgumentNullException.ThrowIfNull(signature);

        return JsonSerializer.Serialize(signature, JsonOptions);
    }
}
