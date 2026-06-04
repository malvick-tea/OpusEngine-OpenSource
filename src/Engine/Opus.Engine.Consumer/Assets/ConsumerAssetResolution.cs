using System;
using System.IO;

namespace Opus.Engine.Consumer.Assets;

/// <summary>
/// Result returned by a consumer asset catalog. A null <see cref="FilePath"/> means the
/// catalog intentionally leaves the request unresolved and the host may fall back to its
/// default path. When a non-null path is supplied the constructor canonicalises it
/// through <see cref="Path.GetFullPath(string)"/> so any caller-supplied <c>..</c>
/// traversal segments are flattened before the host loader opens a handle.
/// </summary>
public sealed record ConsumerAssetResolution
{
    /// <summary>Creates a resolution. Use <see cref="Unresolved"/> when no file is available.</summary>
    public ConsumerAssetResolution(string? filePath)
    {
        FilePath = NormalisePath(filePath);
    }

    /// <summary>Unresolved result.</summary>
    public static ConsumerAssetResolution Unresolved { get; } = new((string?)null);

    /// <summary>Filesystem path to the resolved asset, or null when unresolved.</summary>
    public string? FilePath { get; }

    /// <summary>Returns whether this resolution carries a path.</summary>
    public bool IsResolved => FilePath is not null;

    private static string? NormalisePath(string? filePath)
    {
        if (filePath is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Resolved asset file path must not be empty.", nameof(filePath));
        }

        try
        {
            return Path.GetFullPath(filePath);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException(
                $"Resolved asset file path '{filePath}' is malformed.",
                nameof(filePath),
                ex);
        }
        catch (NotSupportedException ex)
        {
            throw new ArgumentException(
                $"Resolved asset file path '{filePath}' uses an unsupported format.",
                nameof(filePath),
                ex);
        }
        catch (PathTooLongException ex)
        {
            throw new ArgumentException(
                $"Resolved asset file path '{filePath}' exceeds the platform path-length limit.",
                nameof(filePath),
                ex);
        }
    }
}
