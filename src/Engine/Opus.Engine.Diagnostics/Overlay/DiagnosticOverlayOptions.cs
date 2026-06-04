using System;
using Opus.Engine.Diagnostics;

namespace Opus.Engine.Diagnostics.Overlay;

/// <summary>Immutable configuration for the tester diagnostic overlay.</summary>
public sealed record DiagnosticOverlayOptions(
    bool Enabled,
    DiagnosticOverlayLevel Level,
    TimeSpan RefreshInterval,
    int MaxRows,
    DiagnosticOverlayToggleKey ToggleKey)
{
    /// <summary>Default refresh cadence for the alpha diagnostic overlay.</summary>
    public static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>Default row cap per snapshot.</summary>
    public const int DefaultMaxRows = 24;

    /// <summary>Hard upper bound on overlay rows the composer will emit before clamping.</summary>
    public const int MaximumRows = 64;

    /// <summary>Default enabled full overlay for tester builds.</summary>
    public static DiagnosticOverlayOptions Default { get; } = new(
        Enabled: true,
        Level: DiagnosticOverlayLevel.Full,
        RefreshInterval: DefaultRefreshInterval,
        MaxRows: DefaultMaxRows,
        ToggleKey: DiagnosticOverlayToggleKey.F10);

    /// <summary>Returns whether the overlay should draw any panel.</summary>
    public bool ShouldDraw => Enabled && Level != DiagnosticOverlayLevel.Off;

    /// <summary>Validates option values before they enter a host render loop.</summary>
    public void Validate()
    {
        if (RefreshInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RefreshInterval),
                DiagnosticCodes.OverlayConfigurationInvalid + ": RefreshInterval must be positive.");
        }

        if (MaxRows < 1 || MaxRows > MaximumRows)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxRows),
                DiagnosticCodes.OverlayConfigurationInvalid + $": MaxRows must be in [1, {MaximumRows}].");
        }
    }
}
