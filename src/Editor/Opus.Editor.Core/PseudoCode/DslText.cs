using System;
using System.Globalization;
using System.Text;

namespace Opus.Editor.Core;

/// <summary>
/// Shared formatting primitives for the editor's pseudo-code (DSL) writers. The scene, animation-graph, and
/// project mirrors all render the same indented, double-quoted, '\n'-terminated declarative syntax, so the
/// indentation, number, and string-escaping rules live here once. Pure and deterministic.
/// </summary>
internal static class DslText
{
    public const string Indent = "  ";
    public const char Newline = '\n';

    /// <summary>Appends one line indented <paramref name="depth"/> levels (two spaces each), then a newline.</summary>
    public static void Line(StringBuilder sb, int depth, string text)
    {
        for (int i = 0; i < depth; i++)
        {
            sb.Append(Indent);
        }

        sb.Append(text).Append(Newline);
    }

    /// <summary>Formats a float with up to three decimals in invariant culture, normalising -0 to 0.</summary>
    public static string Num(float value)
    {
        float normalised = value == 0f ? 0f : value;
        return normalised.ToString("0.###", CultureInfo.InvariantCulture);
    }

    /// <summary>Wraps a string in double quotes, escaping backslashes and embedded quotes.</summary>
    public static string Quote(string value)
    {
        string escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return "\"" + escaped + "\"";
    }
}
