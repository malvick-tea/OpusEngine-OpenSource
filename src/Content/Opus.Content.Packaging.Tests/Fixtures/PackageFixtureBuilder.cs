using System.Buffers.Binary;
using System.Text;
using Opus.Content.Packaging.Manifest;
using Opus.Content.Packaging.Validation;
using Opus.Content.Sample;
using Opus.Foundation;

namespace Opus.Content.Packaging.Tests.Fixtures;

/// <summary>
/// Disposable temp-directory fixture that builds a package with a generic glTF/PNG/KTX2/
/// TTF/JSON-localisation set and writes a manifest signed with the current Opus identity.
/// Builder methods are chainable; tests override individual manifest fields to exercise
/// failure paths without rebuilding the whole package.
/// </summary>
internal sealed class PackageFixtureBuilder : IDisposable
{
    private readonly DirectoryInfo _directory;
    private readonly List<(string Path, string Type, byte[] Bytes, bool WriteToDisk)> _files = new();
    private readonly List<string> _requiredFeatures = new();
    private ManifestFormatVersion _formatVersion = new(1, 0);
    private string _engineProduct = EngineIdentity.Current.ProductName;
    private string _engineTargetVersion = EngineIdentity.Current.ProductVersion.ToString();

    public PackageFixtureBuilder()
    {
        _directory = Directory.CreateTempSubdirectory("opus-package-fixture-");
    }

    public string Root => _directory.FullName;

    public PackageFixtureBuilder WithGoldenFiles()
    {
        AddFile("models/triangle.glb", PackageAssetTypes.ModelGlb, SampleAlphaTankGltfWriter.BuildGlb());
        AddFile("textures/checker.png", PackageAssetTypes.TexturePng, TinyPngPayload());
        AddFile("textures/minimal.ktx2", PackageAssetTypes.TextureKtx, MinimalKtx2());
        AddFile("fonts/fixture-latin.ttf", PackageAssetTypes.Font, MinimalTrueTypeFont());
        AddFile("localisation/en.json", PackageAssetTypes.LocalisationJson, Encoding.UTF8.GetBytes("""{"sample.title":"Opus","sample.ok":"OK"}"""));
        AddFile("localisation/ru.json", PackageAssetTypes.LocalisationJson, Encoding.UTF8.GetBytes("""{"sample.title":"Опус","sample.ok":"OK"}"""));
        return this;
    }

    public PackageFixtureBuilder WithRequiredFeature(string feature)
    {
        _requiredFeatures.Add(feature);
        return this;
    }

    public PackageFixtureBuilder WithManifestFormatVersion(int major, int minor)
    {
        _formatVersion = new ManifestFormatVersion(major, minor);
        return this;
    }

    public PackageFixtureBuilder WithEngineProduct(string product)
    {
        _engineProduct = product;
        return this;
    }

    public PackageFixtureBuilder WithEngineTargetVersion(string version)
    {
        _engineTargetVersion = version;
        return this;
    }

    public PackageFixtureBuilder AddFile(string path, string type, byte[] bytes)
    {
        var fullPath = Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, bytes);
        _files.Add((path, type, bytes, WriteToDisk: true));
        return this;
    }

    /// <summary>Adds an entry to the manifest with an intentionally unsafe path. No file is
    /// written to disk — the validator must reject the entry before any IO.</summary>
    public PackageFixtureBuilder AddUnsafePath(string declaredPath)
    {
        _files.Add((declaredPath, PackageAssetTypes.TexturePng, Array.Empty<byte>(), WriteToDisk: false));
        return this;
    }

    /// <summary>Re-declares the last manifest entry to test duplicate-path detection.
    /// Does not touch the filesystem; the validator must catch the duplicate from the
    /// manifest alone.</summary>
    public PackageFixtureBuilder DuplicateLastFileEntry()
    {
        if (_files.Count == 0)
        {
            throw new InvalidOperationException("Add at least one file before calling DuplicateLastFileEntry.");
        }

        var last = _files[^1];
        _files.Add(last);
        return this;
    }

    public PackageFixtureBuilder WriteManifest()
    {
        var manifest = new ContentPackageManifest(
            _formatVersion,
            new ContentPackageInfo("vellum.opus.fixtures.alpha", "Opus Alpha Generic Fixtures", "0.1.0-alpha.1", "2026-06-03T12:00:00Z"),
            new ContentPackageTarget(
                _engineProduct,
                _engineTargetVersion,
                EngineIdentity.Current.ProductVersion.ToString(),
                EngineIdentity.Current.AssemblyNamePrefix,
                new[] { "generic", "direct3d12" }),
            new ContentPackageAuthoring(BuildInfoSnapshot()),
            new ContentPackageEntrypoints("models/triangle.glb", new[] { "en", "ru" }),
            _requiredFeatures,
            _files.Select(f => new ContentPackageFile(
                f.Path,
                f.Type,
                f.Bytes.LongLength,
                PackageFileHash.ComputeSha256Hex(f.Bytes),
                null)).ToArray());
        File.WriteAllText(
            Path.Combine(Root, PackageValidator.DefaultManifestFileName),
            ContentPackageManifestReader.Write(manifest));
        return this;
    }

    public void ReplaceFile(string path, byte[] bytes)
    {
        var fullPath = Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar));
        File.WriteAllBytes(fullPath, bytes);
    }

    public void DeleteFile(string path)
    {
        var fullPath = Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar));
        File.Delete(fullPath);
    }

    public void WriteMalformedManifest()
    {
        File.WriteAllText(Path.Combine(Root, PackageValidator.DefaultManifestFileName), "{ not json");
    }

    public void Dispose()
    {
        try
        {
            _directory.Delete(recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; a held file handle from a parallel test should not
            // mask the actual assertion failure.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static byte[] TinyPngPayload() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR42mP8z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC");

    private static ContentPackageBuildInfo BuildInfoSnapshot()
    {
        var info = BuildInfo.Current;
        return new ContentPackageBuildInfo(
            info.Engine.DisplayName,
            info.Engine.ProductVersion.ToString(),
            info.Engine.ReleaseChannel,
            info.ProjectName,
            info.Version.ToString(),
            info.BuildConfiguration,
            info.FrameworkDescription,
            info.OperatingSystem,
            info.ProcessArchitecture);
    }

    private static byte[] MinimalKtx2()
    {
        var bytes = new byte[32];
        new byte[] { 0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24, 4), 1);
        return bytes;
    }

    private static byte[] MinimalTrueTypeFont() => Convert.FromHexString("000100000001000000000000");
}
