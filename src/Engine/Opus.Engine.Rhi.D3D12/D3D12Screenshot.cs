using System;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>CPU-side RGBA8 screenshot captured from a D3D12 texture. The payload is
/// always tightly packed at <see cref="Width"/> × <see cref="Height"/> × 4 bytes; the
/// original GPU-side row pitch is preserved on <see cref="SourceRowPitch"/> for
/// diagnostics.</summary>
public sealed record D3D12Screenshot(
    int Width,
    int Height,
    byte[] Rgba8,
    string SourceFormat,
    int SourceRowPitch)
{
    public int ByteSize => Rgba8.Length;

    /// <summary>Saves a dependency-free 32-bit BMP. Kept for legacy alpha-smoke paths
    /// that need a lossless self-contained dump with zero metadata. Bug-report-grade
    /// captures should use <see cref="SavePng"/> instead.</summary>
    public void SaveBmp(string path) => D3D12ScreenshotBmpWriter.Write(path, this);

    /// <summary>Saves a real PNG with optional embedded metadata as tEXt chunks. PNG is
    /// the alpha-grade capture format for tester bug reports because it is universally
    /// readable, losslessly compressed, and carries the build/adapter/frame identity
    /// inline so a screenshot received by mail is self-describing.</summary>
    public void SavePng(string path, D3D12ScreenshotMetadata? metadata = null) =>
        D3D12ScreenshotPngWriter.Write(path, this, metadata ?? D3D12ScreenshotMetadata.Empty);
}
