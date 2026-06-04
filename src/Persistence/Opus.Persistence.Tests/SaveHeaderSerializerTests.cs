using System.Buffers.Binary;
using FluentAssertions;
using MemoryPack;
using Opus.Foundation;
using Opus.Persistence;
using Xunit;

namespace Opus.Persistence.Tests;

[MemoryPackable]
public partial record FrameTestBody(int Counter, string Tag);

/// <summary>Pure-data verification of <see cref="SaveHeaderSerializer"/> — frame
/// round-trip, magic mismatch detection, structural malformation handling.</summary>
public sealed class SaveHeaderSerializerTests
{
    private static readonly AppVersion SampleVersion = new(2, 5, 1, "alpha", "build42");

    [Fact]
    public void WriteFrame_then_ReadFrame_round_trips_header_and_body()
    {
        IBinaryCodec codec = new MemoryPackCodec();
        var header = SaveHeader.Current(schemaVersion: 7, SampleVersion, unixMs: 1_700_000_000_000L);
        var body = new FrameTestBody(Counter: 99, Tag: "sample");

        var frame = SaveHeaderSerializer.WriteFrame(header, body, codec);
        var read = SaveHeaderSerializer.ReadFrame<FrameTestBody>(frame, codec);

        read.IsOk.Should().BeTrue();
        var (decodedHeader, decodedBody) = read.Unwrap();
        decodedHeader.Magic.Should().Be(SaveHeader.MagicV1);
        decodedHeader.SchemaVersion.Should().Be(7);
        decodedHeader.AuthoringVersion.Should().Be(SampleVersion);
        decodedHeader.CreatedAtUnixMs.Should().Be(1_700_000_000_000L);
        decodedBody.Should().Be(body);
    }

    [Fact]
    public void ReadFrame_rejects_foreign_magic_with_SaveCorrupt()
    {
        IBinaryCodec codec = new MemoryPackCodec();
        var alienHeader = new SaveHeader("FOREIGN", SchemaVersion: 1, SampleVersion, CreatedAtUnixMs: 0);
        var frame = SaveHeaderSerializer.WriteFrame(alienHeader, new FrameTestBody(0, "x"), codec);

        var read = SaveHeaderSerializer.ReadFrame<FrameTestBody>(frame, codec);

        read.IsErr.Should().BeTrue();
        read.UnwrapErr().Code.Should().Be(ErrorCode.SaveCorrupt);
    }

    [Fact]
    public void ReadFrame_returns_SaveCorrupt_on_truncated_frame()
    {
        IBinaryCodec codec = new MemoryPackCodec();
        var header = SaveHeader.Current(1, SampleVersion, unixMs: 0);
        var frame = SaveHeaderSerializer.WriteFrame(header, new FrameTestBody(7, "z"), codec);

        var truncated = new byte[frame.Length / 2];
        System.Buffer.BlockCopy(frame, 0, truncated, 0, truncated.Length);
        var read = SaveHeaderSerializer.ReadFrame<FrameTestBody>(truncated, codec);

        read.IsErr.Should().BeTrue();
        read.UnwrapErr().Code.Should().Be(ErrorCode.SaveCorrupt);
    }

    [Fact]
    public void WriteFrame_handles_empty_strings_in_AppVersion()
    {
        IBinaryCodec codec = new MemoryPackCodec();
        var bareVersion = new AppVersion(1, 0, 0, string.Empty, string.Empty);
        var header = SaveHeader.Current(1, bareVersion, unixMs: 0);

        var frame = SaveHeaderSerializer.WriteFrame(header, new FrameTestBody(0, string.Empty), codec);
        var read = SaveHeaderSerializer.ReadFrame<FrameTestBody>(frame, codec);

        read.IsOk.Should().BeTrue();
        read.Unwrap().Header.AuthoringVersion.Should().Be(bareVersion);
    }

    [Fact]
    public void ReadFrame_rejects_an_oversized_length_prefix_without_allocating()
    {
        IBinaryCodec codec = new MemoryPackCodec();

        // The first 4 bytes are the magic field's length prefix (little-endian). Claim ~2 GiB while
        // supplying only a few trailing bytes: a hostile/corrupt frame must be rejected as
        // SaveCorrupt, not pre-allocate the bogus length (OOM) before noticing the data is absent.
        var frame = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(frame, int.MaxValue);

        var read = SaveHeaderSerializer.ReadFrame<FrameTestBody>(frame, codec);

        read.IsErr.Should().BeTrue();
        read.UnwrapErr().Code.Should().Be(ErrorCode.SaveCorrupt);
    }

    [Fact]
    public void ReadFrame_rejects_a_negative_length_prefix()
    {
        IBinaryCodec codec = new MemoryPackCodec();
        var frame = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(frame, -1);

        var read = SaveHeaderSerializer.ReadFrame<FrameTestBody>(frame, codec);

        read.IsErr.Should().BeTrue();
        read.UnwrapErr().Code.Should().Be(ErrorCode.SaveCorrupt);
    }
}
