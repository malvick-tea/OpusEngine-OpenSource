using System;
using System.IO;
using FluentAssertions;
using Opus.Foundation;
using Xunit;

namespace Opus.Engine.Host.Windows.Direct3D12.Tests.Scene;

public sealed class D3D12AlphaSampleAssetsContentFailureTests
{
    [SkippableFact]
    public void Malformed_consumer_asset_surfaces_as_engine_content_exception()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "D3D12 host tests are Windows-only.");

        var badAssetPath = Path.Combine(Path.GetTempPath(), $"opus-bad-asset-{Guid.NewGuid():N}.glb");
        File.WriteAllBytes(badAssetPath, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77 });
        using var sink = new StringWriter();
        var log = new ConsoleLog(LogLevel.Information, sink, sink, TimeProvider.System);
        var options = D3D12OpusApplicationOptions.Default with
        {
            WindowTitle = "opus-host-bad-asset",
            WindowWidth = 256,
            WindowHeight = 192,
            AssetPath = badAssetPath,
        };

        try
        {
            D3D12OpusHostInstance? instance = null;
            try
            {
                instance = new D3D12OpusHostBuilder().WithLog(log).WithOptions(options).TryBuild();
            }
            catch (EngineContentException ex)
            {
                ex.ContentPath.Should().Be(badAssetPath, "the content boundary tags the failing asset path.");
                return;
            }
            finally
            {
                instance?.Dispose();
            }

            // No exception: either the device was unavailable (null) so the asset boundary
            // was never reached — skip — or a malformed asset was wrongly accepted, which is
            // a real regression.
            Skip.If(instance is null, "D3D12 adapter / SDL video / DXC unavailable on this host.");
            throw new Xunit.Sdk.XunitException(
                "A malformed consumer asset should have surfaced as EngineContentException.");
        }
        finally
        {
            TryDelete(badAssetPath);
        }
    }

    private static void TryDelete(string path)
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
