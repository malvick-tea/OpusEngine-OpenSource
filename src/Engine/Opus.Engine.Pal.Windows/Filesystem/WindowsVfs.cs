using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Opus.Engine.Pal.Filesystem;
using Opus.Foundation;
using Opus.Foundation.IO;

namespace Opus.Engine.Pal.Windows.Filesystem;

/// <summary>Windows filesystem VFS with canonical containment and reparse-point rejection.</summary>
public sealed class WindowsVfs : IVfs
{
    private readonly string _resRoot;
    private readonly string _userRoot;

    public WindowsVfs(string resRoot, string userRoot)
    {
        _resRoot = Path.GetFullPath(Ensure.NotNullOrEmpty(resRoot));
        _userRoot = Path.GetFullPath(Ensure.NotNullOrEmpty(userRoot));
        Directory.CreateDirectory(_userRoot);
    }

    public static WindowsVfs ForCurrentProcess(string productName)
    {
        Ensure.NotNullOrEmpty(productName);

        var resRoot = Path.Combine(AppContext.BaseDirectory, "content");
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        return new WindowsVfs(resRoot, Path.Combine(localAppData, productName));
    }

    public bool Exists(string virtualPath)
    {
        var path = Realize(virtualPath);
        PathContainment.RejectReparsePoints(RootFor(virtualPath), path);
        return File.Exists(path);
    }

    public Stream OpenRead(string virtualPath)
    {
        var path = Realize(virtualPath);
        PathContainment.RejectReparsePoints(RootFor(virtualPath), path);
        return File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public Stream OpenWrite(string virtualPath)
    {
        RequireWritableRoot(virtualPath);
        var path = Realize(virtualPath);
        var directory = RequireParent(path);
        Directory.CreateDirectory(directory);
        PathContainment.RejectReparsePoints(_userRoot, directory);
        return File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
    }

    public async Task WriteAllBytesAtomicAsync(
        string virtualPath,
        byte[] payload,
        CancellationToken ct)
    {
        Ensure.NotNull(payload);
        RequireWritableRoot(virtualPath);

        var path = Realize(virtualPath);
        var directory = RequireParent(path);
        Directory.CreateDirectory(directory);
        PathContainment.RejectReparsePoints(_userRoot, directory);
        await AtomicFile.WriteAllBytesAsync(
            path,
            payload,
            ct).ConfigureAwait(false);
    }

    public string Realize(string virtualPath)
    {
        Ensure.NotNullOrEmpty(virtualPath);
        var root = VirtualPath.ParseScheme(virtualPath);
        var relative = VirtualPath.StripScheme(virtualPath);
        if (VirtualPath.ContainsTraversal(relative))
        {
            throw new ArgumentException(
                $"Path traversal not allowed: '{virtualPath}'",
                nameof(virtualPath));
        }

        var baseDirectory = root switch
        {
            VfsRoot.Res => _resRoot,
            VfsRoot.User => _userRoot,
            _ => throw new InvariantViolationException($"Unhandled root {root}"),
        };

        return PathContainment.ResolveUnderRoot(
            baseDirectory,
            relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string RequireParent(string path) =>
        Path.GetDirectoryName(path)
        ?? throw new InvalidOperationException(
            $"Resolved path '{path}' has no parent directory.");

    private static void RequireWritableRoot(string virtualPath)
    {
        if (VirtualPath.ParseScheme(virtualPath) != VfsRoot.User)
        {
            throw new InvalidOperationException(
                $"Cannot write '{virtualPath}'; only user:// is writable.");
        }
    }

    private string RootFor(string virtualPath) =>
        VirtualPath.ParseScheme(virtualPath) switch
        {
            VfsRoot.Res => _resRoot,
            VfsRoot.User => _userRoot,
            var root => throw new InvariantViolationException(
                $"Unhandled root {root}"),
        };
}
