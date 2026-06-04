using FluentAssertions;
using Opus.Content.Textures;
using Xunit;

namespace Opus.Content.Tests.Textures;

public sealed class MipChainTests
{
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(2, 1, 2)]
    [InlineData(4, 4, 3)]
    [InlineData(8, 2, 4)]
    public void LevelCount_uses_largest_dimension(int width, int height, int expected)
    {
        MipChain.LevelCount(width, height).Should().Be(expected);
    }

    [Fact]
    public void Generate_keeps_base_level_and_box_filters_even_dimensions()
    {
        var baseLevel = new DecodedImage(
            2,
            2,
            new byte[]
            {
                10, 20, 30, 40,
                20, 30, 40, 50,
                30, 40, 50, 60,
                40, 50, 60, 70,
            });

        var levels = MipChain.Generate(baseLevel);

        levels.Should().HaveCount(2);
        levels[0].Should().BeSameAs(baseLevel);
        levels[1].Width.Should().Be(1);
        levels[1].Height.Should().Be(1);
        levels[1].Rgba.Should().Equal((byte)25, (byte)35, (byte)45, (byte)55);
    }

    [Fact]
    public void Generate_rejects_invalid_base_dimensions()
    {
        var act = () => MipChain.Generate(new DecodedImage(0, 1, Array.Empty<byte>()));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
