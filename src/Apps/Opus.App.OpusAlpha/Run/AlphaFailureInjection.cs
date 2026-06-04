using System;
using Opus.App.OpusAlpha.Cli;
using Opus.Foundation;

namespace Opus.App.OpusAlpha.Run;

/// <summary>
/// Turns a requested <see cref="AlphaFaultKind"/> into the engine exception that simulates that
/// failure family, and raises it at the alpha host's startup boundary. This is a tester-facing
/// self-check, not a test-only hook: a tester or lead runs <c>--inject-failure</c> on their own
/// machine to confirm the diagnostics bundle (paired failure report plus log tail) is produced
/// correctly before a real crash happens. The injected exception flows through the same
/// <see cref="Opus.Engine.Diagnostics.Reports.FailureReportClassifier"/> and failure-report
/// path a genuine engine failure would, so the tester validates the real pipeline rather than a
/// stand-in.
/// </summary>
public static class AlphaFailureInjection
{
    /// <summary>Synthetic content identity carried by the injected content failure so the report
    /// shows an unambiguous, clearly-injected origin rather than a real asset path.</summary>
    private const string InjectedContentPath = "injected://content-load-failure";

    /// <summary><c>DXGI_ERROR_DEVICE_HUNG</c>. A representative device-removed reason so the
    /// injected device-lost report carries the same shape a real TDR-style device loss would.</summary>
    private const int InjectedDeviceRemovedReason = unchecked((int)0x887A0006);

    /// <summary>Raises the exception that simulates <paramref name="kind"/> when one is requested,
    /// after logging a warning so both the console and rolling log make clear the failure was
    /// deliberately injected. A no-op for <see cref="AlphaFaultKind.None"/>.</summary>
    public static void ThrowIfRequested(AlphaFaultKind kind, ILog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        var injected = CreateException(kind);
        if (injected is null)
        {
            return;
        }

        log.Warn($"Injecting requested '{kind}' failure to validate the tester diagnostics path.");
        throw injected;
    }

    /// <summary>Maps a fault kind to the engine exception that represents it, or <c>null</c> for
    /// <see cref="AlphaFaultKind.None"/>. Pure: no IO and no clock, so the mapping is unit-testable
    /// in isolation. Throws only for an unrecognised enum value.</summary>
    public static Exception? CreateException(AlphaFaultKind kind) => kind switch
    {
        AlphaFaultKind.None => null,
        AlphaFaultKind.Startup => new InvalidOperationException(
            "Injected startup failure for diagnostics validation."),
        AlphaFaultKind.Content => new EngineContentException(
            "Injected content-load failure for diagnostics validation.",
            InjectedContentPath),
        AlphaFaultKind.DeviceLost => new EngineDeviceLostException(
            "Injected device-lost failure for diagnostics validation.",
            InjectedDeviceRemovedReason),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown alpha fault kind."),
    };
}
