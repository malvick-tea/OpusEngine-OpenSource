using System;
using System.Collections.Generic;

namespace Opus.Engine.Diagnostics.Overlay;

/// <summary>Immutable group of diagnostic rows rendered together in the overlay.</summary>
public sealed record DiagnosticPanel(
    DiagnosticPanelKind Kind,
    string Title,
    IReadOnlyList<DiagnosticRow> Rows)
{
    /// <summary>Creates a validated panel. Rejects null row entries loudly so a misplaced
    /// row from a panel builder cannot silently disappear from the tester overlay.</summary>
    public static DiagnosticPanel Create(
        DiagnosticPanelKind kind,
        string title,
        IEnumerable<DiagnosticRow> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(rows);

        var copy = new List<DiagnosticRow>();
        foreach (var row in rows)
        {
            if (row is null)
            {
                throw new ArgumentException(
                    "Diagnostic panel row list contains a null entry.",
                    nameof(rows));
            }

            copy.Add(row);
        }

        return new DiagnosticPanel(kind, title, copy.ToArray());
    }
}
