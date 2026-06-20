using System.Buffers.Binary;
using System.IO;
using FluentAssertions;
using Opus.Content.Textures;
using Xunit;

namespace Opus.Content.Tests.Textures;

public sealed class ImageDecoderTests
{
    [Fact]
    public void Decode_rejects_an_encoded_blob_over_the_limit()
    {
        var encoded = new byte[ImageDecoder.MaxEncodedBytes + 1];

        var act = () => ImageDecoder.DecodeRgba8(encoded);

        act.Should().Throw<InvalidDataException>().WithMessage("*limit*");
    }

    [Fact]
    public void Decode_rejects_dimensions_over_the_limit_before_pixel_decode()
    {
        var encoded = BuildPngHeader(ImageDecoder.MaxDimension + 1, 1);

        var act = () => ImageDecoder.DecodeRgba8(encoded);

        act.Should().Throw<InvalidDataException>().WithMessage("*safety limits*");
    }

    private static byte[] BuildPngHeader(int width, int height)
    {
        var bytes = new byte[33];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8, 4), 13);
        "IHDR"u8.CopyTo(bytes.AsSpan(12, 4));
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height);
        bytes[24] = 8;
        bytes[25] = 6;
        return bytes;
    }
}
