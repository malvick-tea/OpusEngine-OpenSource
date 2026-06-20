using System.Linq;
using FluentAssertions;
using Opus.Content.Packaging.Paths;
using Opus.Content.Packaging.Validation;
using Xunit;

namespace Opus.Content.Packaging.Tests;

public sealed partial class PackageValidatorTests
{
    [Fact]
    public void Streaming_hash_matches_in_memory_hash()
    {
        var payload = Enumerable.Range(0, 200_000).Select(i => (byte)(i & 0xFF)).ToArray();
        var path = Path.Combine(Path.GetTempPath(), $"opus-hash-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, payload);
        try
        {
            var inMemory = PackageFileHash.ComputeSha256Hex(payload);
            var streamed = PackageFileHash.ComputeSha256HexFile(path);

            streamed.Should().Be(inMemory);
        }
        finally
        {
            File.Delete(path);
        }
    }

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
    [InlineData("a/file:stream", false, null)]
    [InlineData("a/CON.txt", false, null)]
    [InlineData("a/name.", false, null)]
    [InlineData("a/name?.txt", false, null)]
    public void PackageRelativePath_accepts_safe_inputs_and_rejects_unsafe_ones(
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
