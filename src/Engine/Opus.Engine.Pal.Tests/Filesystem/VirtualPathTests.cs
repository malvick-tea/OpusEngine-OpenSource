using System;
using FluentAssertions;
using Opus.Engine.Pal.Filesystem;
using Xunit;

namespace Opus.Engine.Pal.Tests.Filesystem;

public sealed class VirtualPathTests
{
    [Theory]
    [InlineData("res://textures/tank.png", VfsRoot.Res)]
    [InlineData("user://saves/slot0.bin", VfsRoot.User)]
    public void Parses_known_schemes(string path, VfsRoot expected)
    {
        VirtualPath.ParseScheme(path).Should().Be(expected);
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("textures/tank.png")]
    [InlineData("file:///c/foo")]
    public void Rejects_unknown_or_missing_scheme(string path)
    {
        Action act = () => VirtualPath.ParseScheme(path);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Strips_scheme_prefix()
    {
        VirtualPath.StripScheme("res://a/b.png").Should().Be("a/b.png");
        VirtualPath.StripScheme("user://saves/slot0.bin").Should().Be("saves/slot0.bin");
    }

    [Theory]
    [InlineData("a/b/c", false)]
    [InlineData("a/../b", true)]
    [InlineData("..\\sneak", true)]
    public void Detects_traversal_attempts(string relative, bool expected)
    {
        VirtualPath.ContainsTraversal(relative).Should().Be(expected);
    }
}
