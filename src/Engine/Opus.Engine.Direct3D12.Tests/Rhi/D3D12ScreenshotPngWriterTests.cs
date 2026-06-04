using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Opus.Engine.Rhi.Direct3D12;
using Xunit;

namespace Opus.Engine.Direct3D12.Tests.Rhi;

public sealed class D3D12ScreenshotPngWriterTests
{
    [Fact]
    public void Round_trips_pixels_and_metadata_through_a_real_png_file()
    {
        var screenshot = BuildSyntheticScreenshot(width: 4, height: 4);
        var metadata = D3D12ScreenshotMetadata.From(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["opus.product"] = "Opus 0.1-alpha",
            ["opus.adapter"] = "Synthetic Adapter",
            ["opus.frame"] = "42",
        });
        var path = NewTempPath();

        try
        {
            D3D12ScreenshotPngWriter.Write(path, screenshot, metadata);

            var decoded = TestPngReader.Decode(path);
            decoded.Width.Should().Be(4);
            decoded.Height.Should().Be(4);
            decoded.BitDepth.Should().Be(8);
            decoded.ColourType.Should().Be(6);
            decoded.TextEntries.Should().HaveCount(3);
            decoded.TextEntries["opus.product"].Should().Be("Opus 0.1-alpha");
            decoded.TextEntries["opus.adapter"].Should().Be("Synthetic Adapter");
            decoded.TextEntries["opus.frame"].Should().Be("42");
            decoded.DecodedRgba.Should().BeEquivalentTo(screenshot.Rgba8);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Empty_metadata_produces_no_text_chunks()
    {
        var screenshot = BuildSyntheticScreenshot(width: 2, height: 2);
        var path = NewTempPath();

        try
        {
            D3D12ScreenshotPngWriter.Write(path, screenshot, D3D12ScreenshotMetadata.Empty);

            var decoded = TestPngReader.Decode(path);
            decoded.TextEntries.Should().BeEmpty();
            decoded.DecodedRgba.Should().BeEquivalentTo(screenshot.Rgba8);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Rejects_mismatched_payload_size()
    {
        var screenshot = new D3D12Screenshot(4, 4, new byte[16], "rgba8", 16);
        Action act = () =>
            D3D12ScreenshotPngWriter.Write(
                Path.Combine(Path.GetTempPath(), "ignored.png"),
                screenshot,
                D3D12ScreenshotMetadata.Empty);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*RGBA payload*");
    }

    private static string NewTempPath() =>
        Path.Combine(Path.GetTempPath(), $"opus-png-test-{Guid.NewGuid():N}.png");

    private static D3D12Screenshot BuildSyntheticScreenshot(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            pixels[(i * 4) + 0] = (byte)((i * 17) & 0xFF);
            pixels[(i * 4) + 1] = (byte)((i * 31) & 0xFF);
            pixels[(i * 4) + 2] = (byte)((i * 53) & 0xFF);
            pixels[(i * 4) + 3] = 0xFF;
        }

        return new D3D12Screenshot(width, height, pixels, "rgba8", width * 4);
    }

    private sealed record DecodedPng(
        int Width,
        int Height,
        byte BitDepth,
        byte ColourType,
        IReadOnlyDictionary<string, string> TextEntries,
        byte[] DecodedRgba);

    private static class TestPngReader
    {
        private static readonly byte[] Signature =
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        };

        public static DecodedPng Decode(string path)
        {
            using var fs = File.OpenRead(path);
            Span<byte> sig = stackalloc byte[8];
            fs.ReadExactly(sig);
            if (!sig.SequenceEqual(Signature.AsSpan()))
            {
                throw new InvalidDataException("File does not start with the PNG signature.");
            }

            int width = 0, height = 0;
            byte bitDepth = 0, colourType = 0;
            var textEntries = new Dictionary<string, string>(StringComparer.Ordinal);
            using var idat = new MemoryStream();

            Span<byte> header = stackalloc byte[8];
            Span<byte> crc = stackalloc byte[4];
            while (true)
            {
                fs.ReadExactly(header);
                var length = (int)BinaryPrimitives.ReadUInt32BigEndian(header[..4]);
                var type = Encoding.ASCII.GetString(header.Slice(4, 4));
                var data = new byte[length];
                if (length > 0)
                {
                    fs.ReadExactly(data);
                }

                fs.ReadExactly(crc);

                switch (type)
                {
                    case "IHDR":
                        width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(0, 4));
                        height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4, 4));
                        bitDepth = data[8];
                        colourType = data[9];
                        break;
                    case "tEXt":
                        var sep = Array.IndexOf<byte>(data, 0);
                        var keyword = Encoding.Latin1.GetString(data, 0, sep);
                        var value = Encoding.Latin1.GetString(data, sep + 1, data.Length - sep - 1);
                        textEntries[keyword] = value;
                        break;
                    case "IDAT":
                        idat.Write(data, 0, data.Length);
                        break;
                    case "IEND":
                        return InflateAndDecode(idat, width, height, bitDepth, colourType, textEntries);
                }
            }
        }

        private static DecodedPng InflateAndDecode(
            MemoryStream idat,
            int width,
            int height,
            byte bitDepth,
            byte colourType,
            IReadOnlyDictionary<string, string> textEntries)
        {
            idat.Position = 0;
            using var inflated = new MemoryStream();
            using (var zlib = new ZLibStream(idat, CompressionMode.Decompress, leaveOpen: true))
            {
                zlib.CopyTo(inflated);
            }

            var raw = inflated.GetBuffer().AsSpan(0, (int)inflated.Length);
            var stride = width * 4;
            var decoded = new byte[stride * height];
            for (var y = 0; y < height; y++)
            {
                var rowOffset = y * (stride + 1);
                if (raw[rowOffset] != 0)
                {
                    throw new InvalidDataException(
                        $"Test reader expected filter byte 0 on row {y}; got {raw[rowOffset]}.");
                }

                raw.Slice(rowOffset + 1, stride).CopyTo(decoded.AsSpan(y * stride, stride));
            }

            return new DecodedPng(width, height, bitDepth, colourType, textEntries, decoded);
        }
    }
}
