using System;
using Opus.Foundation;

namespace Opus.Engine.Pal.Filesystem;

/// <summary>
/// Thin parsing helper for VFS URIs. Concrete <see cref="IVfs"/> implementations use this
/// to validate inputs and reject paths that escape their root via <c>..</c>.
/// </summary>
public static class VirtualPath
{
    public const string ResScheme = "res://";
    public const string UserScheme = "user://";

    public static VfsRoot ParseScheme(string virtualPath)
    {
        Ensure.NotNullOrEmpty(virtualPath);

        if (virtualPath.StartsWith(ResScheme, StringComparison.Ordinal))
        {
            return VfsRoot.Res;
        }

        if (virtualPath.StartsWith(UserScheme, StringComparison.Ordinal))
        {
            return VfsRoot.User;
        }

        throw new ArgumentException(
            $"Virtual path must start with '{ResScheme}' or '{UserScheme}'; got '{virtualPath}'.",
            nameof(virtualPath));
    }

    public static string StripScheme(string virtualPath)
    {
        var root = ParseScheme(virtualPath);
        return root switch
        {
            VfsRoot.Res => virtualPath.Substring(ResScheme.Length),
            VfsRoot.User => virtualPath.Substring(UserScheme.Length),
            _ => throw new InvariantViolationException($"Unhandled root {root}"),
        };
    }

    public static bool ContainsTraversal(string relative)
    {
        // Reject any segment that resolves above the root. Done as a string check to keep
        // VirtualPath itself zero-allocation for the common case.
        return relative.Contains("..", StringComparison.Ordinal);
    }
}

public enum VfsRoot
{
    Res,
    User,
}
