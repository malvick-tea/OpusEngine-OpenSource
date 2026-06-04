namespace Opus.Engine.Diagnostics.Overlay;

/// <summary>
/// Mutable runtime visibility state for the tester diagnostic overlay, driven by the
/// configured <see cref="DiagnosticOverlayToggleKey"/>. The host maps each platform key
/// edge to a <see cref="DiagnosticOverlayToggleKey"/> and feeds it through
/// <see cref="HandleKeyDown"/> / <see cref="HandleKeyUp"/>; the overlay coordinator reads
/// <see cref="IsEnabled"/> when deciding whether to compose and draw.
/// <para>
/// Rising-edge only: a held toggle key (SDL re-raises key-down as OS auto-repeat) flips the
/// overlay exactly once, on the first key-down, and not again until the key is released and
/// pressed anew. The type is single-threaded by design — it lives behind the host's event
/// pump and render loop on one thread, the same as <see cref="DiagnosticOverlayComposer"/>.
/// </para>
/// </summary>
public sealed class DiagnosticOverlayToggleState
{
    private readonly DiagnosticOverlayToggleKey _toggleKey;
    private bool _enabled;
    private bool _toggleKeyHeld;

    /// <summary>Creates the state bound to <paramref name="toggleKey"/> with the supplied
    /// initial visibility. Pass <see cref="DiagnosticOverlayToggleKey.None"/> to make the
    /// overlay non-toggleable (it stays at <paramref name="initiallyEnabled"/> forever).</summary>
    public DiagnosticOverlayToggleState(DiagnosticOverlayToggleKey toggleKey, bool initiallyEnabled)
    {
        _toggleKey = toggleKey;
        _enabled = initiallyEnabled;
    }

    /// <summary>Whether the overlay is currently switched on.</summary>
    public bool IsEnabled => _enabled;

    /// <summary>Whether a toggle key is configured (anything other than
    /// <see cref="DiagnosticOverlayToggleKey.None"/>).</summary>
    public bool IsToggleConfigured => _toggleKey != DiagnosticOverlayToggleKey.None;

    /// <summary>Feeds a key-down edge. Flips <see cref="IsEnabled"/> and returns <c>true</c>
    /// only on the rising edge of the configured toggle key; OS auto-repeat (key-down with
    /// no intervening key-up) and non-matching keys are ignored and return <c>false</c>.</summary>
    public bool HandleKeyDown(DiagnosticOverlayToggleKey key)
    {
        if (_toggleKey == DiagnosticOverlayToggleKey.None || key != _toggleKey)
        {
            return false;
        }

        if (_toggleKeyHeld)
        {
            return false;
        }

        _toggleKeyHeld = true;
        _enabled = !_enabled;
        return true;
    }

    /// <summary>Feeds a key-up edge so the next key-down for the configured toggle key is
    /// treated as a fresh press rather than auto-repeat.</summary>
    public void HandleKeyUp(DiagnosticOverlayToggleKey key)
    {
        if (key == _toggleKey)
        {
            _toggleKeyHeld = false;
        }
    }
}
