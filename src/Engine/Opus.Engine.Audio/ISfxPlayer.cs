namespace Opus.Engine.Audio;

/// <summary>
/// Fire-and-forget play of a short sound effect. Multiple Play calls for the same path
/// overlap (gunshot fired twice in quick succession plays both samples). The backend
/// resolves the <paramref name="vfsPath"/> via the host's <c>IVfs</c>; caches the loaded
/// sample so repeat plays don't re-read disk.
/// </summary>
/// <remarks>
/// <para>
/// Effective gain = <see cref="AudioMixer.EffectiveSfx"/> × <paramref name="volumeMultiplier"/>,
/// clamped to [0, 1]. The multiplier is per-call (a faraway tank fires quieter, the
/// UI click plays at full volume).
/// </para>
/// <para>
/// Phase-0 ships a flat path-based API. A typed <c>SfxId</c> catalog (so callers don't
/// hardcode <c>"res://audio/sfx/ui_click.wav"</c> strings) lands once the canonical sfx
/// list stabilises post-alpha.
/// </para>
/// </remarks>
public interface ISfxPlayer
{
    /// <summary>Plays <paramref name="vfsPath"/> once. <paramref name="volumeMultiplier"/>
    /// scales the effective sfx gain ([0, 1]; out-of-range values clamped).</summary>
    void Play(string vfsPath, float volumeMultiplier = 1f);
}
