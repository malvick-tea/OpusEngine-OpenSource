using System;
using System.IO;
using FluentAssertions;
using Opus.Engine.Consumer.Assets;
using Xunit;

namespace Opus.Engine.Consumer.Tests.Assets;

public sealed class ConsumerAssetResolutionTests
{
    [Fact]
    public void Unresolved_singleton_carries_no_path()
    {
        ConsumerAssetResolution.Unresolved.IsResolved.Should().BeFalse();
        ConsumerAssetResolution.Unresolved.FilePath.Should().BeNull();
    }

    [Fact]
    public void Resolved_path_is_normalised_through_get_full_path()
    {
        var input = Path.Combine(Path.GetTempPath(), "opus-asset", "..", "asset", "primary.glb");

        var resolution = new ConsumerAssetResolution(input);

        resolution.IsResolved.Should().BeTrue();
        resolution.FilePath.Should().NotContain("..");
        Path.IsPathFullyQualified(resolution.FilePath!).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_path_is_rejected(string filePath)
    {
        Action act = () => _ = new ConsumerAssetResolution(filePath);

        act.Should().Throw<ArgumentException>().WithMessage("*file path*");
    }

    [Fact]
    public void Malformed_path_surfaces_as_argument_exception()
    {
        // Invalid path char on every supported runtime: NUL byte (\0).
        var malformed = "C:/invalid\0path/primary.glb";

        Action act = () => _ = new ConsumerAssetResolution(malformed);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Asset_id_primary_value_constant_matches_static_instance()
    {
        ConsumerAssetId.PrimarySceneModel.Value.Should().Be(ConsumerAssetId.PrimarySceneModelValue);
    }

    [Fact]
    public void Asset_id_rejects_whitespace()
    {
        Action act = () => _ = new ConsumerAssetId("\t");

        act.Should().Throw<ArgumentException>();
    }
}
