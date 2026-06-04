using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Opus.Content.Packaging.Validation;
using Xunit;

namespace Opus.Content.Packaging.Tests;

public sealed class PackageFileHashTests
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
}
