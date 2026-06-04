using System.Numerics;
using FluentAssertions;
using Opus.Content.Animation;
using Xunit;

namespace Opus.Content.Tests.Animation;

public sealed class AnimationSamplerTests
{
    [Fact]
    public void SampleTrack_interpolates_translation_rotation_and_scale()
    {
        var track = new BoneAnimationTrack(
            0,
            new[]
            {
                new TransformKey(0f, Vector3.Zero, Quaternion.Identity, Vector3.One),
                new TransformKey(
                    2f,
                    new Vector3(10f, 0f, 0f),
                    Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI),
                    new Vector3(3f, 3f, 3f)),
            });

        var sampled = AnimationSampler.SampleTrack(track, 1f);

        sampled.M41.Should().BeApproximately(5f, 1e-5f);
        sampled.M11.Should().BeApproximately(0f, 1e-5f);
        sampled.M12.Should().BeApproximately(-2f, 1e-5f);
        sampled.M21.Should().BeApproximately(2f, 1e-5f);
        sampled.M22.Should().BeApproximately(0f, 1e-5f);
    }

    [Fact]
    public void SampleTrack_clamps_before_first_and_after_last_key()
    {
        var track = new BoneAnimationTrack(
            0,
            new[]
            {
                new TransformKey(5f, new Vector3(1f, 2f, 3f), Quaternion.Identity, Vector3.One),
                new TransformKey(10f, new Vector3(4f, 5f, 6f), Quaternion.Identity, Vector3.One),
            });

        var before = AnimationSampler.SampleTrack(track, 0f);
        var after = AnimationSampler.SampleTrack(track, 99f);

        before.M41.Should().BeApproximately(1f, 1e-5f);
        before.M42.Should().BeApproximately(2f, 1e-5f);
        before.M43.Should().BeApproximately(3f, 1e-5f);
        after.M41.Should().BeApproximately(4f, 1e-5f);
        after.M42.Should().BeApproximately(5f, 1e-5f);
        after.M43.Should().BeApproximately(6f, 1e-5f);
    }

    [Fact]
    public void SamplePose_writes_only_tracks_that_exist_and_validates_palette_bounds()
    {
        var clip = new AnimationClip(
            "pose",
            1f,
            new[]
            {
                new BoneAnimationTrack(
                    1,
                    new[] { new TransformKey(0f, new Vector3(7f, 8f, 9f), Quaternion.Identity, Vector3.One) }),
            });
        var palette = new[] { Matrix4x4.Identity, Matrix4x4.Identity };

        AnimationSampler.SamplePose(clip, 0f, palette);

        palette[0].Should().Be(Matrix4x4.Identity);
        palette[1].M41.Should().BeApproximately(7f, 1e-5f);

        var invalid = new AnimationClip("bad", 1f, new[] { new BoneAnimationTrack(3, Array.Empty<TransformKey>()) });
        var act = () => AnimationSampler.SamplePose(invalid, 0f, palette);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
