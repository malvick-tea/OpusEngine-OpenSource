using System;
using System.Numerics;

namespace Opus.Content.Animation;

/// <summary>
/// Samples animation tracks at a given time into a bone-matrix palette compatible with
/// R-10.b's GPU skinning pipeline. Translation/scale use linear interpolation; rotation
/// uses spherical lerp.
/// </summary>
public static class AnimationSampler
{
    /// <summary>
    /// Returns the local transform of a single track at <paramref name="time"/>. Time
    /// is clamped to the keyframe range (no looping).
    /// </summary>
    public static Matrix4x4 SampleTrack(BoneAnimationTrack track, float time)
    {
        if (track.Keys.Length == 0)
        {
            return Matrix4x4.Identity;
        }

        if (track.Keys.Length == 1)
        {
            return BuildTransform(track.Keys[0]);
        }

        if (time <= track.Keys[0].Time)
        {
            return BuildTransform(track.Keys[0]);
        }

        if (time >= track.Keys[^1].Time)
        {
            return BuildTransform(track.Keys[^1]);
        }

        // Linear scan — animation tracks are typically short (<100 keys), no need for binsearch.
        var lowerIdx = 0;
        for (var i = 1; i < track.Keys.Length; i++)
        {
            if (track.Keys[i].Time > time)
            {
                lowerIdx = i - 1;
                break;
            }
        }

        var a = track.Keys[lowerIdx];
        var b = track.Keys[lowerIdx + 1];
        var span = b.Time - a.Time;
        var t = span > 0f ? (time - a.Time) / span : 0f;

        var translation = Vector3.Lerp(a.Translation, b.Translation, t);
        var rotation = Quaternion.Slerp(a.Rotation, b.Rotation, t);
        var scale = Vector3.Lerp(a.Scale, b.Scale, t);
        return Matrix4x4.CreateScale(scale)
             * Matrix4x4.CreateFromQuaternion(rotation)
             * Matrix4x4.CreateTranslation(translation);
    }

    /// <summary>
    /// Writes per-bone sampled transforms into <paramref name="palette"/>. Caller is
    /// responsible for pre-filling palette entries for bones not driven by the clip
    /// (typically <see cref="Matrix4x4.Identity"/> or the bone's bind pose).
    /// </summary>
    public static void SamplePose(AnimationClip clip, float time, Matrix4x4[] palette)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(palette);

        foreach (var track in clip.Tracks)
        {
            if (track.BoneIndex < 0 || track.BoneIndex >= palette.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(clip),
                    $"Track references bone {track.BoneIndex} but palette has {palette.Length} slots.");
            }

            palette[track.BoneIndex] = SampleTrack(track, time);
        }
    }

    private static Matrix4x4 BuildTransform(TransformKey k) =>
        Matrix4x4.CreateScale(k.Scale)
        * Matrix4x4.CreateFromQuaternion(k.Rotation)
        * Matrix4x4.CreateTranslation(k.Translation);
}
