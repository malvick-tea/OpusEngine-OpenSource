using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Opus.Engine.Pal.Filesystem;
using Opus.Foundation;

namespace Opus.Engine.Pal.Windows.Filesystem;

/// <summary>
/// Filesystem VFS for Windows. Resolves <c>res://</c> against the binary directory and
/// <c>user://</c> against <c>%LOCALAPPDATA%\Opus</c>. Per layout v2 §7, the more
/// elaborate variants — package-archive reader (.gpkg), atomic-rename writer with W^X
/// semantics — live alongside this class but ship in later milestones.
/// </summary>
public sealed class WindowsVfs : IVfs
{
    private readonly string _resRoot;
    private readonly string _userRoot;

    public WindowsVfs(string resRoot, string userRoot)
    {
        _resRoot = Ensure.NotNullOrEmpty(resRoot);
        _userRoot = Ensure.NotNullOrEmpty(userRoot);
        Directory.CreateDirectory(_userRoot);
    }

    public static WindowsVfs ForCurrentProcess(string productName)
    {
        Ensure.NotNullOrEmpty(productName);

        var binDir = AppContext.BaseDirectory;
        var resRoot = Path.Combine(binDir, "content");
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userRoot = Path.Combine(localAppData, productName);
        return new WindowsVfs(resRoot, userRoot);
    }

    public bool Exists(string virtualPath) => File.Exists(Realize(virtualPath));

    public Stream OpenRead(string virtualPath) =>
        File.Open(Realize(virtualPath), FileMode.Open, FileAccess.Read, FileShare.Read);

    public Stream OpenWrite(string virtualPath)
    {
        var root = VirtualPath.ParseScheme(virtualPath);
        if (root != VfsRoot.User)
        {
            throw new InvalidOperationException(
                $"Cannot open '{virtualPath}' for write — only user:// is writable.");
        }

        var real = Realize(virtualPath);
        Directory.CreateDirectory(Path.GetDirectoryName(real)!);
        return File.Open(real, FileMode.Create, FileAccess.Write, FileShare.None);
    }

    public async Task WriteAllBytesAtomicAsync(string virtualPath, byte[] payload, CancellationToken ct)
    {
        Ensure.NotNull(payload);
        var root = VirtualPath.ParseScheme(virtualPath);
        if (root != VfsRoot.User)
        {
            throw new InvalidOperationException(
                $"Cannot atomically write '{virtualPath}' — only user:// is writable.");
        }

        var real = Realize(virtualPath);
        Directory.CreateDirectory(Path.GetDirectoryName(real)!);

        var tmp = real + ".tmp." + Guid.NewGuid().ToString("N");
        await File.WriteAllBytesAsync(tmp, payload, ct);

        // Atomic rename across replacements. File.Move with overwrite=true is the closest
        // cross-FS-safe primitive .NET exposes on NTFS; for AAA-grade durability we'd need
        // ReplaceFile + fsync — that lands in the M3 hardening pass.
        File.Move(tmp, real, overwrite: true);
    }

    public string Realize(string virtualPath)
    {
        Ensure.NotNullOrEmpty(virtualPath);
        var root = VirtualPath.ParseScheme(virtualPath);
        var rel = VirtualPath.StripScheme(virtualPath);

        if (VirtualPath.ContainsTraversal(rel))
        {
            throw new ArgumentException($"Path traversal not allowed: '{virtualPath}'", nameof(virtualPath));
        }

        var baseDir = root switch
        {
            VfsRoot.Res => _resRoot,
            VfsRoot.User => _userRoot,
            _ => throw new InvariantViolationException($"Unhandled root {root}"),
        };

        return Path.Combine(baseDir, rel.Replace('/', Path.DirectorySeparatorChar));
    }
}
