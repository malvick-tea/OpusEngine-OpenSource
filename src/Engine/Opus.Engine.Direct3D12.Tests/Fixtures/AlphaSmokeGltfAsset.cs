using System;
using System.IO;
using Opus.Content.Sample;

namespace Opus.Engine.Direct3D12.Tests.Fixtures;

/// <summary>Temp-file wrapper around <see cref="SampleAlphaTankGltfWriter"/>. Writes the
/// sample tank-marker GLB to a uniquely-named file under the OS temp directory and
/// deletes it on <see cref="Dispose"/>. Kept as a fixture so the GLB-emitting concern
/// stays in the reusable content layer while smoke tests still get RAII cleanup.</summary>
internal sealed class AlphaSmokeGltfAsset : IDisposable
{
    private AlphaSmokeGltfAsset(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static AlphaSmokeGltfAsset WriteTempGlb()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"opus-alpha-smoke-{Guid.NewGuid():N}.glb");
        SampleAlphaTankGltfWriter.WriteTo(path);
        return new AlphaSmokeGltfAsset(path);
    }

    public void Dispose()
    {
        try
        {
            File.Delete(Path);
        }
        catch (IOException)
        {
            // Best-effort cleanup — a test crash mid-run can leave the file locked.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
