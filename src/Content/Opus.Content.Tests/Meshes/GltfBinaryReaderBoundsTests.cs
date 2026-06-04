using System.IO;
using System.Text;
using FluentAssertions;
using Opus.Content.Meshes;
using Opus.Content.Tests.Fixtures;
using Xunit;

namespace Opus.Content.Tests.Meshes;

/// <summary>
/// The accessor element count and byte offset come from the glTF JSON, which is untrusted content
/// on the asset-load path. A hostile <c>accessors[i].count</c> must be rejected before any array is
/// allocated, so a malformed model produces an <see cref="InvalidDataException"/> instead of a
/// multi-gigabyte allocation (OOM) or a negative-size allocation crash.
/// </summary>
public sealed class GltfBinaryReaderBoundsTests
{
    // ~2 billion VEC3 elements would be ~24 GiB if allocated; the guard must refuse before that.
    private const int HostileCount = 2_000_000_000;

    [Fact]
    public void ReadScene_rejects_a_vertex_accessor_count_past_the_buffer()
    {
        var glb = GltfTestAssets.PackGlb(
            Encoding.UTF8.GetBytes(PositionOnlyModel(positionCount: HostileCount)),
            new byte[12]);

        var act = () => GltfBinaryReader.ReadScene(glb);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void ReadScene_rejects_an_index_accessor_count_past_the_buffer()
    {
        var glb = GltfTestAssets.PackGlb(
            Encoding.UTF8.GetBytes(IndexedModel(indexCount: HostileCount)),
            new byte[12]);

        var act = () => GltfBinaryReader.ReadScene(glb);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void ReadScene_rejects_a_negative_accessor_count()
    {
        var glb = GltfTestAssets.PackGlb(
            Encoding.UTF8.GetBytes(PositionOnlyModel(positionCount: -1)),
            new byte[12]);

        var act = () => GltfBinaryReader.ReadScene(glb);

        act.Should().Throw<InvalidDataException>();
    }

    private static string PositionOnlyModel(int positionCount) =>
        """
        {
          "asset": { "version": "2.0" },
          "buffers": [{ "byteLength": 12 }],
          "bufferViews": [{ "buffer": 0, "byteOffset": 0, "byteLength": 12 }],
          "accessors": [{ "bufferView": 0, "componentType": 5126, "count": COUNT, "type": "VEC3" }],
          "meshes": [{ "primitives": [{ "attributes": { "POSITION": 0 } }] }]
        }
        """.Replace("COUNT", positionCount.ToString(System.Globalization.CultureInfo.InvariantCulture), System.StringComparison.Ordinal);

    private static string IndexedModel(int indexCount) =>
        """
        {
          "asset": { "version": "2.0" },
          "buffers": [{ "byteLength": 12 }],
          "bufferViews": [{ "buffer": 0, "byteOffset": 0, "byteLength": 12 }],
          "accessors": [
            { "bufferView": 0, "componentType": 5126, "count": 1, "type": "VEC3" },
            { "bufferView": 0, "componentType": 5125, "count": COUNT, "type": "SCALAR" }
          ],
          "meshes": [{ "primitives": [{ "attributes": { "POSITION": 0 }, "indices": 1 }] }]
        }
        """.Replace("COUNT", indexCount.ToString(System.Globalization.CultureInfo.InvariantCulture), System.StringComparison.Ordinal);
}
