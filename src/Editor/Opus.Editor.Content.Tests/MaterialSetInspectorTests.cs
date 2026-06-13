using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Opus.Editor.Content.Tests;

public sealed class MaterialSetInspectorTests
{
    [Fact]
    public void Complete_set_reports_all_four_maps_present()
    {
        var report = MaterialSetInspector.Inspect("root", "brick", _ => true);

        report.MaterialName.Should().Be("brick");
        report.PresentCount.Should().Be(4);
        report.IsComplete.Should().BeTrue();
        report.HasBaseColor.Should().BeTrue();
        report.Maps.Should().OnlyContain(map => map.Present);
    }

    [Fact]
    public void Empty_set_reports_no_maps_and_is_incomplete()
    {
        var report = MaterialSetInspector.Inspect("root", "brick", _ => false);

        report.PresentCount.Should().Be(0);
        report.IsComplete.Should().BeFalse();
        report.HasBaseColor.Should().BeFalse();
    }

    [Fact]
    public void Partial_set_flags_the_missing_maps()
    {
        // Only the base-colour file exists on disk.
        bool Exists(string path) => path.Contains("_basecolor", StringComparison.Ordinal);

        var report = MaterialSetInspector.Inspect("root", "brick", Exists);

        report.PresentCount.Should().Be(1);
        report.HasBaseColor.Should().BeTrue();
        report.IsComplete.Should().BeFalse();
        report.Maps.Should().Contain(map => map.Kind == MaterialMapKind.Normal && !map.Present);
    }

    [Fact]
    public void Probe_receives_the_convention_relative_path()
    {
        var probed = new List<string>();
        MaterialSetInspector.Inspect("root", "brick", path =>
        {
            probed.Add(path);
            return false;
        });

        probed.Should().Contain(path => path.Contains("brick_orm.png", StringComparison.Ordinal));
    }
}
