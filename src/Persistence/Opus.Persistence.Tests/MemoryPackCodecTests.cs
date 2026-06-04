using FluentAssertions;
using MemoryPack;
using Opus.Foundation;
using Opus.Persistence;
using Xunit;

namespace Opus.Persistence.Tests;

[MemoryPackable]
public partial record TestPayload(int Value, string Label);

public sealed class MemoryPackCodecTests
{
    [Fact]
    public void Round_trips_a_simple_record()
    {
        IBinaryCodec codec = new MemoryPackCodec();
        var original = new TestPayload(42, "sample");

        var bytes = codec.Serialize(original);
        bytes.Should().NotBeNullOrEmpty();

        var restored = codec.Deserialize<TestPayload>(bytes);
        restored.IsOk.Should().BeTrue();
        restored.Unwrap().Should().Be(original);
    }

    [Fact]
    public void Returns_err_on_corrupt_input()
    {
        IBinaryCodec codec = new MemoryPackCodec();
        var garbage = new byte[] { 0xFF, 0x42, 0x00, 0x99 };

        var result = codec.Deserialize<TestPayload>(garbage);
        result.IsErr.Should().BeTrue();
        result.UnwrapErr().Code.Should().Be(ErrorCode.SaveCorrupt);
    }
}
