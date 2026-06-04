namespace Opus.Content.Packaging.Paths;

/// <summary>
/// Validated package-relative POSIX-style path. The validator rejects absolute paths,
/// empty segments, parent-directory traversal, and control characters before touching
/// the filesystem.
/// <para>
/// The type is intentionally a value type with a private constructor so the only way to
/// obtain a non-default instance is through <see cref="TryCreate"/>; downstream code can
/// treat a non-default <see cref="PackageRelativePath"/> as a safety invariant.
/// </para>
/// </summary>
public readonly record struct PackageRelativePath
{
    private PackageRelativePath(string value)
    {
        Value = value;
    }

    /// <summary>Normalised path value using <c>/</c> as separator.</summary>
    public string Value { get; }

    /// <summary>
    /// Tries to validate and normalise a manifest path. Rejection reasons returned in
    /// <paramref name="reason"/> are invariant English strings safe to embed in
    /// diagnostics.
    /// </summary>
    public static bool TryCreate(string? text, out PackageRelativePath path, out string reason)
    {
        path = default;
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            reason = "Path is empty.";
            return false;
        }

        // Reject control characters early — a manifest containing 0x00 or a CR/LF inside a
        // file path is a malicious or corrupt manifest, not user error to normalise away.
        foreach (var ch in text)
        {
            if (ch < 0x20 || ch == 0x7F)
            {
                reason = "Path contains control characters.";
                return false;
            }
        }

        var normalised = text.Replace('\\', '/');
        if (normalised.StartsWith('/'))
        {
            reason = "Path must be relative to the package root.";
            return false;
        }

        // Path.IsPathRooted catches platform-specific drive prefixes (e.g. C:foo on Windows)
        // that StartsWith('/') misses. Rejection here keeps the safety invariant identical
        // on every host the validator runs on.
        if (Path.IsPathRooted(normalised))
        {
            reason = "Path must be relative to the package root.";
            return false;
        }

        var parts = normalised.Split('/');
        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                reason = "Path contains an empty segment.";
                return false;
            }

            if (part == "." || part == "..")
            {
                reason = "Path must not contain current or parent-directory segments.";
                return false;
            }
        }

        path = new PackageRelativePath(normalised);
        return true;
    }

    /// <summary>Combines the path with an already trusted package root and returns the
    /// fully-resolved physical path. The caller is responsible for passing an
    /// already-absolute <paramref name="packageRoot"/>; the resulting path is canonical
    /// and safe to hand to filesystem APIs.</summary>
    public string ToPhysicalPath(string packageRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);

        return Path.GetFullPath(
            Path.Combine(packageRoot, Value.Replace('/', Path.DirectorySeparatorChar)));
    }

    public override string ToString() => Value;
}
