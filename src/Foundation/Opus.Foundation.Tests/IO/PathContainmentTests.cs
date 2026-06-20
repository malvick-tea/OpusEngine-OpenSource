using System;
using System.IO;
using FluentAssertions;
using Opus.Foundation.IO;
using Xunit;

namespace Opus.Foundation.Tests.IO;

public sealed class PathContainmentTests
{
    [Fact]
    public void ResolveUnderRoot_accepts_a_normal_relative_path()
    {
        var root = Path.Combine(Path.GetTempPath(), "opus-containment");

        var resolved = PathContainment.ResolveUnderRoot(root, "models/tank.glb");

        PathContainment.IsWithin(root, resolved).Should().BeTrue();
    }

    [Fact]
    public void ResolveUnderRoot_maps_an_empty_relative_path_to_the_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "opus-containment");

        var resolved = PathContainment.ResolveUnderRoot(root, string.Empty);

        resolved.Should().Be(Path.GetFullPath(root));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("folder/../../outside.txt")]
    public void ResolveUnderRoot_rejects_escape(string relativePath)
    {
        var root = Path.Combine(Path.GetTempPath(), "opus-containment");

        var act = () => PathContainment.ResolveUnderRoot(root, relativePath);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("folder/file:stream")]
    [InlineData("folder/name.")]
    [InlineData("folder/name ")]
    [InlineData("folder/CON.txt")]
    [InlineData("folder/name?.txt")]
    public void ResolveUnderRoot_rejects_windows_unsafe_segments(string relativePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "opus-containment");

        var act = () => PathContainment.ResolveUnderRoot(root, relativePath);

        act.Should().Throw<ArgumentException>();
    }
}
