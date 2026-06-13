using System.Numerics;
using FluentAssertions;
using Opus.Editor.Ui;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class GizmoDragTranslationTests
{
    [Fact]
    public void The_axis_parameter_is_where_the_pick_ray_crosses_the_axis()
    {
        // A ray straight down through x = 3 crosses the world X axis at parameter 3.
        var ray = new Ray(new Vector3(3f, 5f, 0f), new Vector3(0f, -1f, 0f));

        GizmoDragTranslation.TryResolveAxisParameter(ray, Vector3.Zero, Vector3.UnitX, out float parameter)
            .Should().BeTrue();
        parameter.Should().BeApproximately(3f, 1e-4f);
    }

    [Fact]
    public void A_ray_parallel_to_the_axis_is_unresolved()
    {
        var ray = new Ray(new Vector3(0f, 1f, 0f), Vector3.UnitX);

        GizmoDragTranslation.TryResolveAxisParameter(ray, Vector3.Zero, Vector3.UnitX, out _)
            .Should().BeFalse("the closest-point solve is ill-conditioned when the ray runs along the axis");
    }
}
