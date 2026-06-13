using System.Collections.Generic;
using System.Numerics;
using Opus.Foundation.Geometry;

namespace Opus.Editor.Ui;

/// <summary>Appends the 12 edges of an axis-aligned box to a line sink, tagged with the given role. Pure.</summary>
public static class WireBox
{
    public static void AppendEdges(ICollection<ViewportLine> sink, Aabb box, ViewportLineRole role)
    {
        var min = box.Min;
        var max = box.Max;
        var corner = new Vector3[8]
        {
            new(min.X, min.Y, min.Z),
            new(max.X, min.Y, min.Z),
            new(max.X, min.Y, max.Z),
            new(min.X, min.Y, max.Z),
            new(min.X, max.Y, min.Z),
            new(max.X, max.Y, min.Z),
            new(max.X, max.Y, max.Z),
            new(min.X, max.Y, max.Z),
        };

        AppendLoop(sink, corner, role, 0, 1, 2, 3);
        AppendLoop(sink, corner, role, 4, 5, 6, 7);
        Edge(sink, corner, 0, 4, role);
        Edge(sink, corner, 1, 5, role);
        Edge(sink, corner, 2, 6, role);
        Edge(sink, corner, 3, 7, role);
    }

    private static void AppendLoop(
        ICollection<ViewportLine> sink, Vector3[] corner, ViewportLineRole role, int a, int b, int c, int d)
    {
        Edge(sink, corner, a, b, role);
        Edge(sink, corner, b, c, role);
        Edge(sink, corner, c, d, role);
        Edge(sink, corner, d, a, role);
    }

    private static void Edge(ICollection<ViewportLine> sink, Vector3[] corner, int a, int b, ViewportLineRole role) =>
        sink.Add(new ViewportLine(corner[a], corner[b], role));
}
