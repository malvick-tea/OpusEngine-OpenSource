using System.Numerics;

namespace Opus.Editor.Core;

/// <summary>
/// An (x, y, z) triple in the editor's document model. A document-facing value type, deliberately
/// distinct from <see cref="Vector3"/>: the on-disk scene format serialises it as named JSON properties
/// (System.Text.Json ignores <see cref="Vector3"/>'s public fields), and the pseudo-code mirror controls
/// its own number formatting. Convert to engine math at the render seam via <see cref="ToVector3"/>.
/// </summary>
/// <param name="X">First component.</param>
/// <param name="Y">Second component.</param>
/// <param name="Z">Third component.</param>
public readonly record struct Float3(float X, float Y, float Z)
{
    public static readonly Float3 Zero = new(0f, 0f, 0f);

    public static readonly Float3 One = new(1f, 1f, 1f);

    public Vector3 ToVector3() => new(X, Y, Z);

    public static Float3 FromVector3(Vector3 v) => new(v.X, v.Y, v.Z);
}
