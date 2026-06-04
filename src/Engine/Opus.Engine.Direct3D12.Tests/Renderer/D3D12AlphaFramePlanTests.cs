using System;
using FluentAssertions;
using Opus.Engine.Renderer.Direct3D12.Alpha;
using Xunit;

namespace Opus.Engine.Direct3D12.Tests.Renderer;

/// <summary>Headless coverage for the alpha-frame plan's size-driven layout — the
/// foundation the M11.13 resize path reuses. A resize regenerates the plan for the new
/// back-buffer size, so the viewport rect and camera aspect must be a pure function of
/// width × height (no GPU needed to verify).</summary>
public sealed class D3D12AlphaFramePlanTests
{
    private const int UiHorizontalMarginPixels = 24;
    private const int UiVerticalMarginPixels = 50;

    [Fact]
    public void Create_insets_the_scene_viewport_from_the_back_buffer()
    {
        var plan = D3D12AlphaFramePlan.Create(800, 600);

        plan.SceneViewport.X.Should().Be(12);
        plan.SceneViewport.Y.Should().Be(36);
        plan.SceneViewport.Width.Should().Be(800 - UiHorizontalMarginPixels);
        plan.SceneViewport.Height.Should().Be(600 - UiVerticalMarginPixels);
    }

    [Fact]
    public void Create_derives_camera_aspect_from_the_viewport_rect()
    {
        var plan = D3D12AlphaFramePlan.Create(1024, 768);

        var expectedAspect = (1024 - UiHorizontalMarginPixels) / (float)(768 - UiVerticalMarginPixels);
        plan.Cameras.Main.AspectRatio.Should().BeApproximately(expectedAspect, 1e-5f);
    }

    [Fact]
    public void Create_regenerates_a_distinct_layout_per_size()
    {
        var small = D3D12AlphaFramePlan.Create(640, 480);
        var large = D3D12AlphaFramePlan.Create(1280, 720);

        large.SceneViewport.Should().NotBe(small.SceneViewport);
        large.Cameras.Main.AspectRatio.Should().NotBe(small.Cameras.Main.AspectRatio);
    }

    [Fact]
    public void Create_keeps_the_deterministic_scene_population_independent_of_size()
    {
        var small = D3D12AlphaFramePlan.Create(640, 480);
        var large = D3D12AlphaFramePlan.Create(1280, 720);

        large.OpponentTanks.Count.Should().Be(small.OpponentTanks.Count);
        large.MapInstanceCount.Should().Be(small.MapInstanceCount);
    }

    [Fact]
    public void Create_rejects_a_back_buffer_below_the_minimum()
    {
        var tooNarrow = () => D3D12AlphaFramePlan.Create(D3D12AlphaFramePlan.MinimumBackBufferWidth - 1, 600);
        var tooShort = () => D3D12AlphaFramePlan.Create(800, D3D12AlphaFramePlan.MinimumBackBufferHeight - 1);

        tooNarrow.Should().Throw<ArgumentOutOfRangeException>();
        tooShort.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Minimum_back_buffer_constants_match_the_documented_floor()
    {
        D3D12AlphaFramePlan.MinimumBackBufferWidth.Should().Be(160);
        D3D12AlphaFramePlan.MinimumBackBufferHeight.Should().Be(120);
    }
}
