using System.Collections.Generic;

namespace Opus.Content.Textures;

/// <summary>A fully block-compressed 2D texture ready for GPU upload: the top-level dimensions plus
/// one tightly-packed BC blob per mip level, level 0 first. Each entry of <see cref="MipBlocks"/> is
/// the exact per-subresource byte layout the D3D12 upload path copies — row-major 4×4 blocks — so
/// block compression stays transparent to the uploader, which derives the block row pitch from the
/// GPU format via <c>GetCopyableFootprints</c>.</summary>
public sealed record CompressedTexture(
    int Width,
    int Height,
    BlockCompressionFormat Format,
    IReadOnlyList<byte[]> MipBlocks);
