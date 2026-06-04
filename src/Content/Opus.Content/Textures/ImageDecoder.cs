using System;
using System.IO;
using StbImageSharp;

namespace Opus.Content.Textures;

/// <summary>Decoded pixel buffer in 32-bit RGBA (R8 G8 B8 A8 unorm, tightly packed,
/// no row padding). One byte = one channel; size = <see cref="Width"/> ×
/// <see cref="Height"/> × 4.</summary>
public sealed record DecodedImage(int Width, int Height, byte[] Rgba)
{
    public int ByteSize => Rgba.Length;

    public int RowPitchBytes => Width * 4;
}

/// <summary>Thin wrapper over StbImageSharp that turns a PNG or JPEG byte blob into a
/// <see cref="DecodedImage"/> in tightly-packed RGBA8. Used by both the glTF embedded-
/// texture path (<see cref="GltfImageReader"/> blobs) and the standalone asset loader
/// once we have one. Throws <see cref="InvalidDataException"/> on a corrupt header or
/// unsupported format — never <see cref="System.Exception"/> from the codec directly.</summary>
public static class ImageDecoder
{
    public static DecodedImage DecodeRgba8(ReadOnlySpan<byte> encoded)
    {
        if (encoded.IsEmpty)
        {
            throw new InvalidDataException("Image blob is empty.");
        }

        ImageResult result;
        try
        {
            result = ImageResult.FromMemory(encoded.ToArray(), ColorComponents.RedGreenBlueAlpha);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("StbImageSharp failed to decode image bytes.", ex);
        }

        if (result.Data is null || result.Width <= 0 || result.Height <= 0)
        {
            throw new InvalidDataException($"Decoded image has invalid dimensions ({result.Width}×{result.Height}).");
        }

        return new DecodedImage(result.Width, result.Height, result.Data);
    }
}
