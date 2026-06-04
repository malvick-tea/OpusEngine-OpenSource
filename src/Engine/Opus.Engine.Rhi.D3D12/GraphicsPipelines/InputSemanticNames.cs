namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Null-terminated UTF-8 semantic names. Statically allocated so callers can
/// take <c>fixed (byte* p = Position)</c> without worrying about literal lifetime.</summary>
internal static class InputSemanticNames
{
    public static readonly byte[] Position = "POSITION\0"u8.ToArray();
    public static readonly byte[] Normal = "NORMAL\0"u8.ToArray();
    public static readonly byte[] Tangent = "TANGENT\0"u8.ToArray();
    public static readonly byte[] Texcoord = "TEXCOORD\0"u8.ToArray();
    public static readonly byte[] Color = "COLOR\0"u8.ToArray();
    public static readonly byte[] BlendIndices = "BLENDINDICES\0"u8.ToArray();
    public static readonly byte[] BlendWeight = "BLENDWEIGHT\0"u8.ToArray();
}
