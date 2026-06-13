using System;
using System.Collections.Generic;
using System.Text;

namespace Opus.Editor.Core;

/// <summary>
/// Projects an <see cref="EditorProjectDocument"/> into the Opus editor's readable pseudo-code: a clean
/// declarative listing of the project's content roots, scenes, animation graphs, and material roots. A
/// pure function of the document — identical input yields identical text, with '\n' line endings — so it
/// is the reliable "what this project contains" view and is fully unit-testable.
/// </summary>
public static class EditorProjectDslWriter
{
    public static string Write(EditorProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var sb = new StringBuilder();
        sb.Append("project ").Append(DslText.Quote(document.Name)).Append(" {").Append(DslText.Newline);
        WriteEntries(sb, "content-root", document.ContentRoots);
        WriteEntries(sb, "scene", document.Scenes);
        WriteEntries(sb, "animgraph", document.AnimationGraphs);
        WriteEntries(sb, "material-root", document.MaterialRoots);
        sb.Append('}').Append(DslText.Newline);
        return sb.ToString();
    }

    private static void WriteEntries(StringBuilder sb, string keyword, IReadOnlyList<string> paths)
    {
        foreach (string path in paths)
        {
            DslText.Line(sb, 1, keyword + " " + DslText.Quote(path));
        }
    }
}
