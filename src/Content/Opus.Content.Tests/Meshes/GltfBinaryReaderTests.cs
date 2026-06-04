using System.IO;
using FluentAssertions;
using Opus.Content.Meshes;
using Opus.Content.Tests.Fixtures;
using Xunit;

namespace Opus.Content.Tests.Meshes;

public sealed class GltfBinaryReaderTests
{
    [Fact]
    public void ReadScene_rejects_truncated_json_chunk_with_invalid_data_exception()
    {
        var glb = GltfTestAssets.TruncatedJsonChunkGlb();

        var act = () => GltfBinaryReader.ReadScene(glb);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*JSON*");
    }
}
