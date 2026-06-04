using System;

namespace Opus.Engine.Diagnostics.Reports;

/// <summary>Adapter and resolution snapshot attached to a failure report when available.</summary>
public sealed record FailureReportAdapterSnapshot(
    string AdapterName,
    int BackBufferWidth,
    int BackBufferHeight)
{
    /// <summary>Adapter hardware identity (vendor, ids, VRAM, class). Defaults to
    /// <see cref="DiagnosticAdapterHardware.Unknown"/> so the mandatory
    /// <see cref="Unavailable"/> sentinel and existing callers stay source-compatible;
    /// the live host populates it when an adapter exists.</summary>
    public DiagnosticAdapterHardware Hardware { get; init; } = DiagnosticAdapterHardware.Unknown;

    /// <summary>Snapshot used when startup failed before a renderer adapter existed.</summary>
    public static FailureReportAdapterSnapshot Unavailable { get; } = new("unavailable", 0, 0);

    /// <summary>Creates a validated adapter snapshot with no hardware identity.</summary>
    public static FailureReportAdapterSnapshot Create(
        string adapterName,
        int backBufferWidth,
        int backBufferHeight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterName);
        if (backBufferWidth < 0 || backBufferHeight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(backBufferWidth), "Back buffer dimensions must not be negative.");
        }

        return new FailureReportAdapterSnapshot(adapterName, backBufferWidth, backBufferHeight);
    }

    /// <summary>Creates a validated adapter snapshot carrying the live adapter hardware
    /// identity.</summary>
    public static FailureReportAdapterSnapshot Create(
        string adapterName,
        int backBufferWidth,
        int backBufferHeight,
        DiagnosticAdapterHardware hardware)
    {
        ArgumentNullException.ThrowIfNull(hardware);
        return Create(adapterName, backBufferWidth, backBufferHeight) with { Hardware = hardware };
    }
}
