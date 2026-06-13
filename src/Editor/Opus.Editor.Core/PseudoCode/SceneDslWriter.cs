using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Opus.Editor.Core;

/// <summary>
/// Projects an <see cref="EditorSceneDocument"/> into the Opus editor's readable pseudo-code: a clean,
/// declarative, engine-neutral mirror of the scene that updates live as the document changes and reads
/// the same whether shown in the editor or hand-written into a scene file. A pure function of the
/// document — identical input yields identical text, with '\n' line endings for cross-platform stability
/// — so it is the reliable "what the engine will build" view and is fully unit-testable.
/// </summary>
public static class SceneDslWriter
{
    public static string Write(EditorSceneDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var sb = new StringBuilder();
        sb.Append("scene ").Append(DslText.Quote(document.Name)).Append(" {").Append(DslText.Newline);

        // Walk the tree from the roots, nesting each node's children inside its block. A flat scene (no node
        // carrying a parent) makes every node a root printed at depth 1, so the output is byte-identical to
        // the pre-hierarchy mirror. The visited set then catches any node left unreached by a malformed
        // parent cycle and prints it as a root, so every node appears exactly once.
        var visited = new HashSet<SceneNodeId>();
        foreach (var node in document.Nodes)
        {
            if (SceneHierarchy.IsRoot(document.Nodes, node))
            {
                WriteNode(sb, document.Nodes, node, 1, visited);
            }
        }

        foreach (var node in document.Nodes)
        {
            if (!visited.Contains(node.Id))
            {
                WriteNode(sb, document.Nodes, node, 1, visited);
            }
        }

        foreach (var light in document.Lights)
        {
            WriteLight(sb, light);
        }

        sb.Append('}').Append(DslText.Newline);
        return sb.ToString();
    }

    private static void WriteNode(
        StringBuilder sb, IReadOnlyList<SceneNode> nodes, SceneNode node, int depth, HashSet<SceneNodeId> visited)
    {
        if (!visited.Add(node.Id))
        {
            return;
        }

        int field = depth + 1;
        DslText.Line(sb, depth, "node " + DslText.Quote(node.Name) + " {");
        DslText.Line(sb, field, "id " + node.Id.Value.ToString(CultureInfo.InvariantCulture));
        if (node.Hidden)
        {
            // Printed only when set, so a mirror without hidden elements is byte-identical to before.
            DslText.Line(sb, field, "hidden true");
        }

        if (node.AssetRef is not null)
        {
            DslText.Line(sb, field, "asset " + DslText.Quote(node.AssetRef));
        }

        DslText.Line(sb, field, "position " + Vec(node.Transform.Position));
        DslText.Line(sb, field, "rotation " + Vec(node.Transform.RotationEulerDegrees));
        DslText.Line(sb, field, "scale " + Vec(node.Transform.Scale));
        foreach (var child in SceneHierarchy.ChildrenOf(nodes, node.Id))
        {
            WriteNode(sb, nodes, child, field, visited);
        }

        DslText.Line(sb, depth, "}");
    }

    private static void WriteLight(StringBuilder sb, SceneLight light)
    {
        DslText.Line(sb, 1, "light " + DslText.Quote(light.Name) + " {");
        DslText.Line(sb, 2, "id " + light.Id.Value.ToString(CultureInfo.InvariantCulture));
        if (light.Hidden)
        {
            DslText.Line(sb, 2, "hidden true");
        }

        DslText.Line(sb, 2, "kind " + KindKeyword(light.Kind));
        DslText.Line(sb, 2, "color " + Vec(light.Color));
        DslText.Line(sb, 2, "intensity " + DslText.Num(light.Intensity));
        if (light.Kind != SceneLightKind.Directional)
        {
            DslText.Line(sb, 2, "position " + Vec(light.Position));
        }

        if (light.Kind != SceneLightKind.Point)
        {
            DslText.Line(sb, 2, "direction " + Vec(light.Direction));
        }

        if (light.Kind != SceneLightKind.Directional)
        {
            DslText.Line(sb, 2, "range " + DslText.Num(light.Range));
        }

        if (light.Kind == SceneLightKind.Spot)
        {
            string cone = "cone (" + DslText.Num(light.SpotInnerAngleDegrees) + ", "
                + DslText.Num(light.SpotOuterAngleDegrees) + ")";
            DslText.Line(sb, 2, cone);
        }

        DslText.Line(sb, 1, "}");
    }

    private static string KindKeyword(SceneLightKind kind) => kind switch
    {
        SceneLightKind.Directional => "directional",
        SceneLightKind.Point => "point",
        SceneLightKind.Spot => "spot",
        _ => "directional",
    };

    private static string Vec(Float3 v) =>
        "(" + DslText.Num(v.X) + ", " + DslText.Num(v.Y) + ", " + DslText.Num(v.Z) + ")";
}
