namespace Opus.Editor.Content;

/// <summary>
/// Per-mesh entry in a <see cref="ModelInspection"/>: the mesh name and its primitive, vertex, and
/// triangle totals. Engine-neutral, read-only reporting data.
/// </summary>
/// <param name="Name">Mesh name from the source file.</param>
/// <param name="PrimitiveCount">Renderable sub-surfaces (separate material / index ranges).</param>
/// <param name="VertexCount">Total vertices across the mesh's primitives.</param>
/// <param name="TriangleCount">Total triangles across the mesh's primitives.</param>
public sealed record ModelMeshSummary(string Name, int PrimitiveCount, int VertexCount, int TriangleCount);
