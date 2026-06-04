using System;
using FluentAssertions;
using Opus.Engine.Consumer.Scene;
using Xunit;

namespace Opus.Engine.Consumer.Tests.Scene;

public sealed class ConsumerViewportSnapshotTests
{
    [Theory]
    [InlineData(0, 720, 1024, 600)]
    [InlineData(1280, 0, 1024, 600)]
    [InlineData(1280, 720, 0, 600)]
    [InlineData(1280, 720, 1024, 0)]
    [InlineData(-1, 720, 1024, 600)]
    public void Non_positive_dimension_is_rejected(int bbWidth, int bbHeight, int sceneWidth, int sceneHeight)
    {
        Action act = () => _ = new ConsumerViewportSnapshot(bbWidth, bbHeight, sceneWidth, sceneHeight);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Valid_dimensions_are_preserved()
    {
        var viewport = new ConsumerViewportSnapshot(1920, 1080, 1024, 600);

        viewport.BackBufferWidth.Should().Be(1920);
        viewport.BackBufferHeight.Should().Be(1080);
        viewport.SceneViewportWidth.Should().Be(1024);
        viewport.SceneViewportHeight.Should().Be(600);
    }
}
