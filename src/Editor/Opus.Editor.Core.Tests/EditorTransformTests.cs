using System;
using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Opus.Editor.Core.Tests;

public sealed class EditorTransformTests
{
    [Fact]
    public void Identity_is_zero_translation_zero_rotation_unit_scale()
    {
        EditorTransform.Identity.Position.Should().Be(Float3.Zero);
        EditorTransform.Identity.RotationEulerDegrees.Should().Be(Float3.Zero);
        EditorTransform.Identity.Scale.Should().Be(Float3.One);
    }

    [Fact]
    public void Identity_rotation_is_the_identity_quaternion()
    {
        var q = EditorTransform.Identity.ToRotationQuaternion();

        q.X.Should().BeApproximately(0f, 1e-5f);
        q.Y.Should().BeApproximately(0f, 1e-5f);
        q.Z.Should().BeApproximately(0f, 1e-5f);
        MathF.Abs(q.W).Should().BeApproximately(1f, 1e-5f);
    }

    [Fact]
    public void Ninety_degree_yaw_rotates_the_x_axis_into_the_z_plane()
    {
        var q = new EditorTransform(Float3.Zero, new Float3(0f, 90f, 0f), Float3.One).ToRotationQuaternion();

        var rotated = Vector3.Transform(Vector3.UnitX, q);

        rotated.X.Should().BeApproximately(0f, 1e-4f);
        rotated.Y.Should().BeApproximately(0f, 1e-4f);
        MathF.Abs(rotated.Z).Should().BeApproximately(1f, 1e-4f);
    }

    [Fact]
    public void Matrix_carries_the_translation()
    {
        var transform = new EditorTransform(new Float3(3f, 4f, 5f), Float3.Zero, Float3.One);

        transform.ToMatrix().Translation.Should().Be(new Vector3(3f, 4f, 5f));
    }
}
