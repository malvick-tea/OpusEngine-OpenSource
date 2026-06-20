using System;
using System.IO;
using StbImageSharp;

namespace Opus.Content.Textures;

/// <summary>Decoded tightly packed RGBA8 pixel data.</summary>
public sealed record DecodedImage(int Width, int Height, byte[] Rgba)
{
    public int ByteSize => Rgba.Length;

    public int RowPitchBytes => checked(Width * 4);
}

/// <summary>Bounded PNG and JPEG decoding through StbImageSharp.</summary>
public static class ImageDecoder
{
    public const int MaxEncodedBytes = 64 * 1024 * 1024;
    public const int MaxDimension = 8192;
    public const long MaxPixelCount = 16L * 1024 * 1024;

    public static DecodedImage DecodeRgba8(ReadOnlySpan<byte> encoded)
    {
        if (encoded.IsEmpty)
        {
            throw new InvalidDataException("Image blob is empty.");
        }

        if (encoded.Length > MaxEncodedBytes)
        {
            throw new InvalidDataException(
                $"Encoded image exceeds the {MaxEncodedBytes}-byte limit.");
        }

        var encodedBytes = encoded.ToArray();
        ImageInfo info;
        try
        {
            using var infoStream = new MemoryStream(encodedBytes, writable: false);
            var inspected = ImageInfo.FromStream(infoStream);
            if (inspected is not ImageInfo validInfo)
            {
                throw new InvalidDataException(
                    "StbImageSharp did not recognise the image header.");
            }

            info = validInfo;
        }
        catch (Exception ex)
        {
            throw new InvalidDataException(
                "StbImageSharp failed to inspect image bytes.",
                ex);
        }

        ValidateDimensions(info.Width, info.Height);

        ImageResult result;
        try
        {
            result = ImageResult.FromMemory(
                encodedBytes,
                ColorComponents.RedGreenBlueAlpha);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException(
                "StbImageSharp failed to decode image bytes.",
                ex);
        }

        if (result.Data is null)
        {
            throw new InvalidDataException("Decoded image has no pixel payload.");
        }

        ValidateDimensions(result.Width, result.Height);
        var expectedBytes = checked((int)((long)result.Width * result.Height * 4L));
        if (result.Data.Length != expectedBytes)
        {
            throw new InvalidDataException(
                $"Decoded image payload is {result.Data.Length} bytes; expected {expectedBytes}.");
        }

        return new DecodedImage(result.Width, result.Height, result.Data);
    }

    private static void ValidateDimensions(int width, int height)
    {
        var pixels = (long)width * height;
        if (width <= 0
            || height <= 0
            || width > MaxDimension
            || height > MaxDimension
            || pixels > MaxPixelCount)
        {
            throw new InvalidDataException(
                $"Image dimensions {width}x{height} exceed decoder safety limits.");
        }
    }
}
