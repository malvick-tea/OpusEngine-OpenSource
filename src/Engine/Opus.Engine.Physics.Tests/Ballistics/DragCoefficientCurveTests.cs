using FluentAssertions;
using Opus.Engine.Physics.Ballistics;
using Xunit;

namespace Opus.Engine.Physics.Tests.Ballistics;

public sealed class DragCoefficientCurveTests
{
    [Fact]
    public void Piecewise_curve_interpolates_between_mach_samples()
    {
        var curve = new PiecewiseLinearDragCoefficientCurve(new[]
        {
            new DragCoefficientPoint(0f, 0.2f),
            new DragCoefficientPoint(2f, 0.4f),
        });

        curve.CoefficientAtMach(1f).Should().BeApproximately(0.3f, 0.0001f);
    }

    [Fact]
    public void Piecewise_curve_clamps_outside_sample_range()
    {
        var curve = new PiecewiseLinearDragCoefficientCurve(new[]
        {
            new DragCoefficientPoint(0.5f, 0.2f),
            new DragCoefficientPoint(1.5f, 0.4f),
        });

        curve.CoefficientAtMach(0f).Should().Be(0.2f);
        curve.CoefficientAtMach(4f).Should().Be(0.4f);
    }
}
