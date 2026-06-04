namespace Opus.Engine.AlphaHarness.Scenes;

/// <summary>Named scene-density presets the M9 harness exposes to its consumers. The host
/// composes the corresponding <see cref="AlphaSceneScaleProfile"/> and passes the
/// per-axis instance counts to the renderer. The enum is the user-facing knob; the
/// profile is the value object that survives serialisation and option records.</summary>
public enum AlphaSceneScale
{
    /// <summary>Compact scene matching the M5/M5.1 smoke shape (~100 repeated actors).
    /// Used by CI and quick local smokes.</summary>
    Small,

    /// <summary>Large-map scene (~300 repeated actors) sized to stress camera, batching,
    /// memory, and frame pacing — the M9 large-map deliverable.</summary>
    Large,

    /// <summary>Massive scene (~1500+ repeated actors) sized for the M12 stress baseline
    /// when consumers need an order-of-magnitude bigger ceiling than <see cref="Large"/>.</summary>
    Massive,
}
