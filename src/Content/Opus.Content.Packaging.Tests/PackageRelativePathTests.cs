using FluentAssertions;
using Opus.Content.Packaging.Paths;
using Xunit;

namespace Opus.Content.Packaging.Tests;

public sealed class PackageRelativePathTests
{
    [Theory]
    [InlineData("name.txt", true, null)]
    [InlineData("dir/inner.txt", true, null)]
    [InlineData("dir\\inner.txt", true, "dir/inner.txt")]
    [InlineData("", false, null)]
    [InlineData("  ", false, null)]
    [InlineData("/abs.txt", false, null)]
    [InlineData("../escape.txt", false, null)]
    [InlineData("a/./b.txt", false, null)]
    [InlineData("a/b\0.txt", false, null)]
    public void Accepts_safe_inputs_and_rejects_unsafe_ones(
        string input,
        bool expectedValid,
        string? expectedNormalised)
    {
        var ok = PackageRelativePath.TryCreate(input, out var path, out _);

        ok.Should().Be(expectedValid);
        if (expectedValid)
        {
            path.Value.Should().Be(expectedNormalised ?? input);
        }
    }
}
