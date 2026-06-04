using System;
using FluentAssertions;
using Opus.Engine.AlphaStress.Memory;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.Memory;

public sealed class MemoryProbeThresholdsTests
{
    [Fact]
    public void Default_validates_cleanly()
    {
        var defaults = MemoryProbeThresholds.Default;

        var act = defaults.Validate;

        act.Should().NotThrow();
    }

    [Fact]
    public void Default_exposes_documented_constants()
    {
        var defaults = MemoryProbeThresholds.Default;

        defaults.ManagedHeapGrowthLimitBytes.Should().Be(MemoryProbeThresholds.DefaultManagedHeapGrowthLimitBytes);
        defaults.WorkingSetGrowthLimitBytes.Should().Be(MemoryProbeThresholds.DefaultWorkingSetGrowthLimitBytes);
        defaults.Gen2CollectionLimit.Should().Be(MemoryProbeThresholds.DefaultGen2CollectionLimit);
    }

    [Fact]
    public void Validate_rejects_non_positive_managed_growth_limit()
    {
        var thresholds = MemoryProbeThresholds.Default with { ManagedHeapGrowthLimitBytes = 0 };

        var act = thresholds.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("ManagedHeapGrowthLimitBytes");
    }

    [Fact]
    public void Validate_rejects_non_positive_working_set_growth_limit()
    {
        var thresholds = MemoryProbeThresholds.Default with { WorkingSetGrowthLimitBytes = 0 };

        var act = thresholds.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("WorkingSetGrowthLimitBytes");
    }

    [Fact]
    public void Validate_rejects_negative_gen2_collection_limit()
    {
        var thresholds = MemoryProbeThresholds.Default with { Gen2CollectionLimit = -1 };

        var act = thresholds.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("Gen2CollectionLimit");
    }
}
