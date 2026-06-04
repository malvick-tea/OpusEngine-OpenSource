namespace Opus.Content.Textures;

/// <summary>The block-compressed GPU texture formats the engine encodes PBR material maps to.
/// <c>Bc7</c> is a high-quality 8-bit-per-channel RGBA format (base colour, packed ORM, emissive);
/// <c>Bc5</c> is a two-channel format for tangent-space normals (stores X and Y, the shader
/// reconstructs Z). Both pack a 4×4 texel block into 16 bytes — a 4:1 ratio against RGBA8 that keeps
/// a city's worth of 4K material sets inside the VRAM budget the uncompressed loader blew. sRGB
/// versus linear sampling is a GPU-view concern, not an encoder concern, so it is not modelled here —
/// the renderer pairs each kind with the matching <c>RhiTextureFormat</c> when it creates the
/// resource.</summary>
public enum BlockCompressionFormat
{
    Bc7,
    Bc5,
}
