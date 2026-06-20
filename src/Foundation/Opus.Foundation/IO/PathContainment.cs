using System;
using System.IO;

namespace Opus.Foundation.IO;

/// <summary>Canonical path resolution for untrusted relative references.</summary>
public static class PathContainment
{
    private static readonly char[] PortableInvalidSegmentCharacters =
        ['<', '>', '"', '|', '?', '*'];

    public static string ResolveUnderRoot(string root, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(relativePath);
        if (relativePath.Length == 0)
        {
            return Path.GetFullPath(root);
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException(
                "A whitespace-only relative path is not allowed.",
                nameof(relativePath));
        }

        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("A rooted path is not allowed.", nameof(relativePath));
        }

        if (OperatingSystem.IsWindows())
        {
            RejectUnsafeWindowsSegments(relativePath);
        }

        var canonicalRoot = Path.GetFullPath(root);
        var candidate = Path.GetFullPath(Path.Combine(canonicalRoot, relativePath));
        if (!IsWithin(canonicalRoot, candidate))
        {
            throw new ArgumentException("The path escapes its configured root.", nameof(relativePath));
        }

        return candidate;
    }

    public static bool IsWithin(string root, string candidate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidate);

        var canonicalRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var canonicalCandidate = Path.GetFullPath(candidate);
        if (string.Equals(canonicalRoot, canonicalCandidate, PathComparison))
        {
            return true;
        }

        return canonicalCandidate.StartsWith(
            canonicalRoot + Path.DirectorySeparatorChar,
            PathComparison);
    }

    public static void RejectReparsePoints(string root, string candidate)
    {
        var canonicalRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var canonicalCandidate = Path.GetFullPath(candidate);
        if (!IsWithin(canonicalRoot, canonicalCandidate))
        {
            throw new UnauthorizedAccessException("The path escapes its configured root.");
        }

        var relative = Path.GetRelativePath(canonicalRoot, canonicalCandidate);
        var current = canonicalRoot;
        foreach (var segment in relative.Split(
                     new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current))
            {
                continue;
            }

            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new UnauthorizedAccessException(
                    $"Reparse points are not allowed in contained paths: '{current}'.");
            }
        }
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static void RejectUnsafeWindowsSegments(string relativePath)
    {
        foreach (var segment in relativePath.Split(
                     new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment.EndsWith(' ')
                || segment.EndsWith('.')
                || segment.Contains(':', StringComparison.Ordinal)
                || segment.IndexOfAny(PortableInvalidSegmentCharacters) >= 0
                || ContainsControlCharacter(segment)
                || IsWindowsDeviceName(segment))
            {
                throw new ArgumentException(
                    "The path contains a Windows-unsafe segment.",
                    nameof(relativePath));
            }
        }
    }

    private static bool ContainsControlCharacter(string segment)
    {
        foreach (var character in segment)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWindowsDeviceName(string segment)
    {
        var stem = segment.Split('.')[0];
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return stem.Length == 4
            && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
            && stem[3] is >= '1' and <= '9';
    }
}
