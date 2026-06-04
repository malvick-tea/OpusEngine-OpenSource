using System;

namespace Opus.Engine.Diagnostics.Overlay;

/// <summary>One immutable key/value row in a diagnostic overlay panel.</summary>
public sealed record DiagnosticRow(string Label, string Value, DiagnosticRowKind Kind)
{
    /// <summary>Creates a validated row.</summary>
    public static DiagnosticRow Create(string label, string value, DiagnosticRowKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(value);
        return new DiagnosticRow(label, value, kind);
    }
}
