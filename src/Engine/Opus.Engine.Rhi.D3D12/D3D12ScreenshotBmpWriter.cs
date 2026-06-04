using System;
using System.Buffers.Binary;
using System.IO;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Writes a <see cref="D3D12Screenshot"/> out as a dependency-free 32-bit BMP.
/// Retained from the M5 alpha smoke for cases that need a zero-metadata, lossless dump
/// (e.g., a quick eyeball check). Runtime bug-report captures use the PNG path so
/// build identity travels with the image.</summary>
public static class D3D12ScreenshotBmpWriter
{
    private const int FileHeaderBytes = 14;
    private const int DibHeaderBytes = 40;
    private const int HeaderBytes = FileHeaderBytes + DibHeaderBytes;
    private const int BytesPerPixel = 4;

    public static void Write(string path, D3D12Screenshot screenshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(screenshot);

        var expectedBytes = checked(screenshot.Width * screenshot.Height * BytesPerPixel);
        if (screenshot.Rgba8.Length != expectedBytes)
        {
            throw new ArgumentException(
                $"Screenshot RGBA payload is {screenshot.Rgba8.Length} bytes; expected {expectedBytes}.",
                nameof(screenshot));
        }

        var pixelBytes = expectedBytes;
        var fileSize = checked(HeaderBytes + pixelBytes);
        var bytes = new byte[fileSize];

        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(2, 4), fileSize);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(10, 4), HeaderBytes);

        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14, 4), DibHeaderBytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18, 4), screenshot.Width);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22, 4), -screenshot.Height);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26, 2), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28, 2), 32);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(34, 4), pixelBytes);

        for (var i = 0; i < screenshot.Width * screenshot.Height; i++)
        {
            var src = i * BytesPerPixel;
            var dst = HeaderBytes + src;
            bytes[dst + 0] = screenshot.Rgba8[src + 2];
            bytes[dst + 1] = screenshot.Rgba8[src + 1];
            bytes[dst + 2] = screenshot.Rgba8[src + 0];
            bytes[dst + 3] = screenshot.Rgba8[src + 3];
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, bytes);
    }
}
