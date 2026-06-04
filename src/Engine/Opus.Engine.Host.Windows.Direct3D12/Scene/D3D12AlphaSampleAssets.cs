using System;
using System.IO;
using Opus.Content.Sample;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Opus.Engine.Rhi.Direct3D12;
using Opus.Foundation;

namespace Opus.Engine.Host.Windows.Direct3D12.Scene;

/// <summary>Owns the glTF asset the alpha host renders, including the procedural
/// fallback path when no consumer asset is configured. Composed by
/// <see cref="D3D12OpusApplication"/>; disposes the underlying
/// <see cref="GarageSceneAssets"/> and (if generated) deletes the temp sample GLB.</summary>
public sealed class D3D12AlphaSampleAssets : IDisposable
{
    private readonly string? _generatedPath;
    private bool _disposed;

    private D3D12AlphaSampleAssets(GarageSceneAssets assets, string sourcePath, bool generated)
    {
        Assets = assets;
        SourcePath = sourcePath;
        _generatedPath = generated ? sourcePath : null;
    }

    public GarageSceneAssets Assets { get; }

    /// <summary>Filesystem path the assets were loaded from. For the procedural fallback
    /// this is a unique temp-directory file; otherwise it is the caller-supplied path.</summary>
    public string SourcePath { get; }

    public bool IsProceduralFallback => _generatedPath is not null;

    /// <summary>Loads the alpha-frame asset. When <paramref name="assetPath"/> is null or
    /// the file does not exist, a procedural sample tank-marker GLB is emitted to a
    /// uniquely-named temp file via <see cref="SampleAlphaTankGltfWriter"/> and that path
    /// is loaded instead. The temp file is cleaned up on <see cref="Dispose"/>.</summary>
    public static D3D12AlphaSampleAssets Load(D3D12RhiDevice device, string namePrefix, string? assetPath)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(namePrefix);

        if (!string.IsNullOrWhiteSpace(assetPath) && File.Exists(assetPath))
        {
            try
            {
                var loaded = GarageSceneAssets.Load(device, assetPath, namePrefix);
                return new D3D12AlphaSampleAssets(loaded, assetPath, generated: false);
            }
            catch (Exception ex) when (ex is not EngineContentException)
            {
                // Translate a consumer-supplied asset decode/read failure into the named
                // content-boundary exception so the failure report is classified as a
                // content failure rather than a generic crash. The procedural fallback below
                // is engine-generated, so a failure there stays a raw throw (an engine bug,
                // not user content).
                throw new EngineContentException(
                    $"Failed to load alpha-host scene asset from '{assetPath}'.", assetPath, ex);
            }
        }

        var generatedPath = Path.Combine(
            Path.GetTempPath(),
            $"opus-alpha-host-sample-{Guid.NewGuid():N}.glb");
        SampleAlphaTankGltfWriter.WriteTo(generatedPath);

        try
        {
            var loaded = GarageSceneAssets.Load(device, generatedPath, namePrefix);
            return new D3D12AlphaSampleAssets(loaded, generatedPath, generated: true);
        }
        catch
        {
            TryDeleteGenerated(generatedPath);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Assets.Dispose();
        if (_generatedPath is not null)
        {
            TryDeleteGenerated(_generatedPath);
        }

        _disposed = true;
    }

    private static void TryDeleteGenerated(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
