using System;
using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class ViewportPickerTests
{
    [Fact]
    public void Picks_the_nearer_of_two_boxes()
    {
        var ray = new Ray(new Vector3(0f, 0f, -10f), Vector3.UnitZ);
        var near = new PickCandidate(new SceneNodeId(1), new Aabb(new Vector3(-1f, -1f, -3f), new Vector3(1f, 1f, -1f)));
        var far = new PickCandidate(new SceneNodeId(2), new Aabb(new Vector3(-1f, -1f, 3f), new Vector3(1f, 1f, 5f)));

        var result = ViewportPicker.Pick(ray, new[] { far, near });

        result.Hit.Should().BeTrue();
        result.Id.Should().Be(new SceneNodeId(1));
    }

    [Fact]
    public void Misses_when_no_candidate_is_hit()
    {
        var ray = new Ray(new Vector3(0f, 0f, -10f), Vector3.UnitZ);
        var offToTheSide = new PickCandidate(
            new SceneNodeId(1), new Aabb(new Vector3(50f, 50f, 50f), new Vector3(51f, 51f, 51f)));

        var result = ViewportPicker.Pick(ray, new[] { offToTheSide });

        result.Hit.Should().BeFalse();
        result.Id.Should().Be(SceneNodeId.None);
    }

    [Fact]
    public void An_empty_candidate_list_is_a_miss()
    {
        var result = ViewportPicker.Pick(new Ray(Vector3.Zero, Vector3.UnitZ), Array.Empty<PickCandidate>());

        result.Hit.Should().BeFalse();
    }
}
