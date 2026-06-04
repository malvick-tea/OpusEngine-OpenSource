using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Opus.Engine.Diagnostics.Overlay;

/// <summary>
/// Composes immutable diagnostic overlay panels from host runtime snapshots.
/// <para>
/// The composer is stateless: every <see cref="Compose"/> call returns a fresh snapshot
/// derived only from its inputs, so call sites can rebuild the overlay on a rate-limit
/// timer without synchronising the composer. Hot-path allocations exist (string formatting
/// and per-panel arrays) and are flagged as the prototype seam to address in M7.x if
/// profiling shows overlay churn.
/// </para>
/// </summary>
public sealed class DiagnosticOverlayComposer
{
    /// <summary>Title rendered above the runtime/build identity panel.</summary>
    public const string RuntimePanelTitle = "runtime";

    /// <summary>Title rendered above the frame/adapter panel.</summary>
    public const string FramePanelTitle = "frame";

    /// <summary>Title rendered above the content counts panel.</summary>
    public const string ContentPanelTitle = "content";

    /// <summary>Title rendered above the network state panel.</summary>
    public const string NetworkPanelTitle = "network";

    /// <summary>Label shown when no successful screenshot has been captured yet.</summary>
    public const string MissingScreenshotLabel = "none";

    /// <summary>Label shown when the host is rendering the procedural sample asset.</summary>
    public const string ProceduralAssetLabel = "procedural";

    /// <summary>Label shown when the host is rendering a consumer-supplied asset.</summary>
    public const string SuppliedAssetLabel = "supplied";

    private const int RuntimePanelCapacity = 4;
    private const int FramePanelCapacity = 9;
    private const int ContentPanelCapacity = 4;
    private const int NetworkPanelCapacity = 2;
    private const int InitialPanelCapacity = 4;

    /// <summary>Composes a snapshot according to the requested overlay options.</summary>
    public DiagnosticOverlaySnapshot Compose(
        DiagnosticOverlayInputs inputs,
        DiagnosticOverlayOptions options)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        if (!options.ShouldDraw)
        {
            return DiagnosticOverlaySnapshot.Empty;
        }

        var panels = new List<DiagnosticPanel>(InitialPanelCapacity)
        {
            BuildRuntimePanel(inputs),
            BuildFramePanel(inputs),
        };

        if (options.Level >= DiagnosticOverlayLevel.Full)
        {
            panels.Add(BuildContentPanel(inputs));
            panels.Add(BuildNetworkPanel(inputs));

            // Consumer-supplied panels come last, after the engine's own diagnostics, and
            // are subject to the same MaxRows budget below so the overlay stays bounded.
            // Inputs are validated at DiagnosticOverlayInputs.Create (null panels filtered),
            // so the composer trusts the list here.
            panels.AddRange(inputs.ConsumerPanels);
        }

        return LimitRows(DiagnosticOverlaySnapshot.Create(panels), options.MaxRows);
    }

    private static DiagnosticPanel BuildRuntimePanel(DiagnosticOverlayInputs inputs)
    {
        var build = inputs.Build;
        var rows = new DiagnosticRow[RuntimePanelCapacity];
        rows[0] = DiagnosticRow.Create("engine", build.Engine.ToIdentityLine(), DiagnosticRowKind.Text);
        rows[1] = DiagnosticRow.Create("assembly", build.ProjectName, DiagnosticRowKind.Text);
        rows[2] = DiagnosticRow.Create("config", build.BuildConfiguration, DiagnosticRowKind.Text);
        rows[3] = DiagnosticRow.Create("screenshot", FormatPath(inputs.LastScreenshotPath), DiagnosticRowKind.Path);
        return DiagnosticPanel.Create(DiagnosticPanelKind.Runtime, RuntimePanelTitle, rows);
    }

    private static DiagnosticPanel BuildFramePanel(DiagnosticOverlayInputs inputs)
    {
        var frame = inputs.FrameMetrics;
        var adapter = inputs.Adapter;
        var rows = new DiagnosticRow[FramePanelCapacity];
        rows[0] = DiagnosticRow.Create("cpu mean", FormatMs(frame.Mean), DiagnosticRowKind.Timing);
        rows[1] = DiagnosticRow.Create(
            "cpu min/p95/max",
            FormatTriplet(frame.Min, frame.P95, frame.Max),
            DiagnosticRowKind.Timing);
        rows[2] = DiagnosticRow.Create(
            "samples",
            frame.SampleCount.ToString(CultureInfo.InvariantCulture),
            DiagnosticRowKind.Count);
        rows[3] = DiagnosticRow.Create(
            "frames",
            frame.TotalFramesObserved.ToString(CultureInfo.InvariantCulture),
            DiagnosticRowKind.Count);
        rows[4] = DiagnosticRow.Create("adapter", adapter.AdapterName, DiagnosticRowKind.Text);
        rows[5] = DiagnosticRow.Create(
            "backbuffer",
            FormatSize(adapter.BackBufferWidth, adapter.BackBufferHeight),
            DiagnosticRowKind.Text);
        rows[6] = DiagnosticRow.Create(
            "viewport",
            FormatSize(adapter.SceneViewportWidth, adapter.SceneViewportHeight),
            DiagnosticRowKind.Text);
        rows[7] = DiagnosticRow.Create(
            "vram",
            FormatVram(adapter.Hardware.DedicatedVideoMemoryMegabytes),
            DiagnosticRowKind.Text);
        rows[8] = DiagnosticRow.Create(
            "vendor",
            FormatVendor(adapter.Hardware),
            DiagnosticRowKind.Text);
        return DiagnosticPanel.Create(DiagnosticPanelKind.Frame, FramePanelTitle, rows);
    }

    private static DiagnosticPanel BuildContentPanel(DiagnosticOverlayInputs inputs)
    {
        var content = inputs.Content;
        var assetLabel = content.UsesProceduralFallback ? ProceduralAssetLabel : SuppliedAssetLabel;
        var rows = new DiagnosticRow[ContentPanelCapacity];
        rows[0] = DiagnosticRow.Create(
            "draw items",
            content.SubmittedDrawItems.ToString(CultureInfo.InvariantCulture),
            DiagnosticRowKind.Count);
        rows[1] = DiagnosticRow.Create(
            "scene instances",
            content.SceneInstanceCount.ToString(CultureInfo.InvariantCulture),
            DiagnosticRowKind.Count);
        rows[2] = DiagnosticRow.Create("asset kind", assetLabel, DiagnosticRowKind.State);
        rows[3] = DiagnosticRow.Create("asset", FormatPath(content.AssetSource), DiagnosticRowKind.Path);
        return DiagnosticPanel.Create(DiagnosticPanelKind.Content, ContentPanelTitle, rows);
    }

    private static DiagnosticPanel BuildNetworkPanel(DiagnosticOverlayInputs inputs)
    {
        var rows = new DiagnosticRow[NetworkPanelCapacity];
        rows[0] = DiagnosticRow.Create(
            "state",
            inputs.Network.State.ToString(),
            DiagnosticRowKind.State);
        rows[1] = DiagnosticRow.Create("detail", inputs.Network.Detail, DiagnosticRowKind.Text);
        return DiagnosticPanel.Create(DiagnosticPanelKind.Network, NetworkPanelTitle, rows);
    }

    private static DiagnosticOverlaySnapshot LimitRows(DiagnosticOverlaySnapshot snapshot, int maxRows)
    {
        var remaining = maxRows;
        var panels = new List<DiagnosticPanel>(snapshot.Panels.Count);
        foreach (var panel in snapshot.Panels)
        {
            if (remaining <= 0)
            {
                break;
            }

            var rowsToKeep = Math.Min(remaining, panel.Rows.Count);
            var rows = new DiagnosticRow[rowsToKeep];
            for (var i = 0; i < rowsToKeep; i++)
            {
                rows[i] = panel.Rows[i];
            }

            panels.Add(DiagnosticPanel.Create(panel.Kind, panel.Title, rows));
            remaining -= rowsToKeep;
        }

        return DiagnosticOverlaySnapshot.Create(panels);
    }

    private static string FormatMs(TimeSpan value) => string.Create(
        CultureInfo.InvariantCulture,
        $"{value.TotalMilliseconds:F2} ms");

    private static string FormatTriplet(TimeSpan min, TimeSpan p95, TimeSpan max) => string.Create(
        CultureInfo.InvariantCulture,
        $"{min.TotalMilliseconds:F2}/{p95.TotalMilliseconds:F2}/{max.TotalMilliseconds:F2} ms");

    private static string FormatSize(int width, int height) => string.Create(
        CultureInfo.InvariantCulture,
        $"{width}x{height}");

    private static string FormatVram(long megabytes) => string.Create(
        CultureInfo.InvariantCulture,
        $"{megabytes} MB");

    private static string FormatVendor(DiagnosticAdapterHardware hardware) => string.Create(
        CultureInfo.InvariantCulture,
        $"{hardware.VendorName} ({hardware.Class.ToString().ToLowerInvariant()})");

    private static string FormatPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return MissingScreenshotLabel;
        }

        var fileName = Path.GetFileName(path);
        return string.IsNullOrWhiteSpace(fileName) ? path : fileName;
    }
}
