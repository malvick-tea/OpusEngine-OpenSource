using System;
using System.Collections.Generic;

namespace Opus.Engine.Diagnostics.Overlay;

/// <summary>Immutable diagnostic overlay snapshot ready for a renderer binding.</summary>
public sealed record DiagnosticOverlaySnapshot(IReadOnlyList<DiagnosticPanel> Panels)
{
    /// <summary>Empty snapshot used when the overlay is disabled.</summary>
    public static DiagnosticOverlaySnapshot Empty { get; } = new(Array.Empty<DiagnosticPanel>());

    /// <summary>Creates a validated snapshot. Rejects null panel entries loudly instead of
    /// silently shrinking the snapshot — a missed panel kind would otherwise look like an
    /// intentionally empty overlay during diagnostics review.</summary>
    public static DiagnosticOverlaySnapshot Create(IEnumerable<DiagnosticPanel> panels)
    {
        ArgumentNullException.ThrowIfNull(panels);
        var copy = new List<DiagnosticPanel>();
        foreach (var panel in panels)
        {
            if (panel is null)
            {
                throw new ArgumentException(
                    "Diagnostic overlay snapshot panel list contains a null entry.",
                    nameof(panels));
            }

            copy.Add(panel);
        }

        return copy.Count == 0 ? Empty : new DiagnosticOverlaySnapshot(copy.ToArray());
    }
}
