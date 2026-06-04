using System;
using FluentAssertions;
using Opus.Engine.Host.Windows.Direct3D12.Scene;
using Xunit;

namespace Opus.Engine.Host.Windows.Direct3D12.Tests.Scene;

public sealed class D3D12AlphaScenePopulationTests
{
    [Fact]
    public void Default_matches_m5_small_smoke_shape()
    {
        var defaults = D3D12AlphaScenePopulation.Default;

        defaults.OpponentColumns.Should().Be(10);
        defaults.OpponentRows.Should().Be(8);
        defaults.ProjectileTrails.Should().Be(12);
        defaults.Casings.Should().Be(16);
    }

    [Fact]
    public void Default_validates_cleanly()
    {
        D3D12AlphaScenePopulation.Default.Invoking(p => p.Validate()).Should().NotThrow();
    }

    [Theory]
    [InlineData(0, 1, 0, 0)]
    [InlineData(1, 0, 0, 0)]
    [InlineData(1, 1, -1, 0)]
    [InlineData(1, 1, 0, -1)]
    public void Validate_rejects_invalid_dimensions(int columns, int rows, int trails, int casings)
    {
        var population = new D3D12AlphaScenePopulation(columns, rows, trails, casings);

        population.Invoking(p => p.Validate()).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Larger_population_accepts_high_density_grid()
    {
        var population = new D3D12AlphaScenePopulation(20, 16, 36, 48);

        population.Invoking(p => p.Validate()).Should().NotThrow();
        (population.OpponentColumns * population.OpponentRows).Should().Be(320);
    }
}
