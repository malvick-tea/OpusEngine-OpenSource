using System.Numerics;

namespace Opus.Content.Animation;

/// <summary>
/// A single keyframe in an animation track — bone local TRS at a moment in time.
/// </summary>
public readonly record struct TransformKey(
    float Time,
    Vector3 Translation,
    Quaternion Rotation,
    Vector3 Scale);

/// <summary>
/// Animation track for one bone. Keys are expected sorted by ascending
/// <see cref="TransformKey.Time"/> — the sampler trusts the ordering and does not
/// validate at sample time.
/// </summary>
public sealed record BoneAnimationTrack(int BoneIndex, TransformKey[] Keys);

/// <summary>
/// A named animation clip — a duration and a set of per-bone tracks. Bones with no
/// track keep their identity (or whatever the caller pre-filled in the palette).
/// </summary>
public sealed record AnimationClip(string Name, float Duration, BoneAnimationTrack[] Tracks);
