using System.IO;
using FluentAssertions;
using Opus.App.Editor.Run;
using Opus.Content.Sample;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorModelBoundsSourceTests
{
    [Fact]
    public void A_ref_under_the_only_root_resolves_to_its_real_bounds()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.File("models"));
        File.WriteAllBytes(temp.File(Path.Combine("models", "tank.glb")), SampleAlphaTankGltfWriter.BuildGlb());
        var source = new EditorModelBoundsSource(temp.Root);

        var bounds = source.TryGetLocalBounds("models/tank.glb");

        bounds.Should().NotBeNull();
        bounds!.Value.Max.X.Should().BeGreaterThan(bounds.Value.Min.X, "a real model has volume");
    }

    [Fact]
    public void With_several_roots_a_ref_resolves_from_the_first_root_holding_it()
    {
        using var first = new TempDirectory();
        using var second = new TempDirectory();
        File.WriteAllBytes(second.File("tank.glb"), SampleAlphaTankGltfWriter.BuildGlb());
        var source = new EditorModelBoundsSource(new[] { first.Root, second.Root });

        source.TryGetLocalBounds("tank.glb").Should().NotBeNull(
            "the first root misses, so the second root's model resolves");
    }

    [Fact]
    public void A_ref_no_root_resolves_returns_null()
    {
        using var temp = new TempDirectory();
        var source = new EditorModelBoundsSource(new[] { temp.Root });

        source.TryGetLocalBounds("missing.glb").Should().BeNull();
    }
}
