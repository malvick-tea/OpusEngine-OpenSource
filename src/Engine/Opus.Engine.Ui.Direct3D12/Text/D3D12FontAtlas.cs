using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;
using Opus.Engine.Ui.Text;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Ui.Direct3D12.Text;

/// <summary>
/// GPU-resident font atlas. Wraps a baked R8 coverage texture, its dedicated shader-visible
/// SRV heap, and the per-codepoint metrics consumed by <see cref="UiTextLayout"/>. One
/// instance lives across the whole session — bake at cold start, keep alive until shutdown.
/// <para>
/// Construction goes through <see cref="BuildAndUpload"/>: the caller hands in the union of
/// localised strings the build ships, the atlas computes the codepoint set, opens the
/// system Latin + CJK faces, bakes them, and uploads. Internal GPU handles (SRV heap +
/// descriptor) are not part of the public surface — only <see cref="D3D12DrawSurface"/>
/// binds them.
/// </para>
/// </summary>
public sealed unsafe class D3D12FontAtlas : IDisposable
{
    /// <summary>Default bake size. Matches the Engine.Ui.Raylib bilingual atlas height so
    /// the two backends rasterise at the same level of detail.</summary>
    public const float DefaultPixelHeight = 24f;

    /// <summary>Default atlas dimensions. 1024×1024 fits Latin + Cyrillic + the kana +
    /// pre-alpha localised kanji at 24-pixel bake height with room to grow.</summary>
    public const int DefaultAtlasSize = 1024;

    private const string AtlasDebugName = "ui.font.atlas";
    private const string UploadListDebugName = "ui.font.atlas.upload";

    private readonly BakedGlyphAtlas _bake;
    private readonly D3D12Texture _texture;
    private readonly GpuDescriptorHandle _srvGpu;
    private ID3D12DescriptorHeap* _srvHeap;
    private bool _disposed;

    private D3D12FontAtlas(BakedGlyphAtlas bake, D3D12Texture texture, ID3D12DescriptorHeap* srvHeap, GpuDescriptorHandle srvGpu)
    {
        _bake = bake;
        _texture = texture;
        _srvHeap = srvHeap;
        _srvGpu = srvGpu;
    }

    public int Width => _bake.Width;

    public int Height => _bake.Height;

    public float BakePixelHeight => _bake.BakePixelHeight;

    public float Ascent => _bake.Ascent;

    public float LineHeight => _bake.LineHeight;

    internal BakedGlyphAtlas Bake => _bake;

    internal Vector2 WhiteUv => _bake.WhiteUv;

    internal ID3D12DescriptorHeap* SrvHeap => _srvHeap;

    internal GpuDescriptorHandle SrvGpu => _srvGpu;

    /// <summary>End-to-end bake + upload. Walks <paramref name="localizedText"/> for the
    /// codepoints to cover, opens the bundled Roboto Latin / Cyrillic face (deterministic across
    /// machines, ADR-0034; system faces are only a fallback if the embedded resource is stripped)
    /// plus the host's CJK system face, bakes them onto an <paramref name="atlasSize"/>² texture at
    /// <paramref name="pixelHeight"/>, uploads. Throws <see cref="InvalidOperationException"/> when
    /// no Latin or CJK face is available, or <see cref="GlyphAtlasOverflowException"/> when the
    /// glyph set does not fit.</summary>
    public static D3D12FontAtlas BuildAndUpload(
        D3D12RhiDevice device,
        IEnumerable<string> localizedText,
        float pixelHeight = DefaultPixelHeight,
        int atlasSize = DefaultAtlasSize)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(localizedText);

        using var latin = BundledFonts.TryOpenLatinFace()
            ?? SystemFontLoader.LoadFirstAvailable(FontFaceCandidates.Latin)
            ?? throw new InvalidOperationException(
                "No Latin font face available: bundled Roboto resource missing and no system fallback opened.");
        using var cjk = SystemFontLoader.LoadFirstAvailable(FontFaceCandidates.Cjk)
            ?? throw new InvalidOperationException("No CJK font face available on this host.");

        var codepoints = FontCodepoints.ForLocalizedText(localizedText);
        var bake = GlyphAtlasBaker.Bake(codepoints, latin, cjk, pixelHeight, atlasSize, atlasSize);
        return Upload(device, bake);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_srvHeap != null)
        {
            _srvHeap->Release();
            _srvHeap = null;
        }

        _texture.Dispose();
    }

    /// <summary>Uploads <paramref name="bake"/> to a freshly-created R8_UNORM texture on
    /// <paramref name="device"/>. Runs a one-shot command list to record the staging copy
    /// + CopyDest→PixelShaderResource transition, drains the GPU, releases staging.</summary>
    internal static D3D12FontAtlas Upload(D3D12RhiDevice device, BakedGlyphAtlas bake)
    {
        var texture = device.CreateGraphicsTexture(new RhiTextureDescription(
            AtlasDebugName,
            bake.Width,
            bake.Height,
            1,
            RhiTextureFormat.R8Unorm,
            RhiTextureUsage.Sampled));

        var srvHeap = device.CreateSrvDescriptorHeap(1u);
        var srvGpu = device.CreateShaderResourceView(texture, srvHeap);

        using var cmdList = device.CreateGraphicsCommandList(UploadListDebugName, frameSlots: 1);
        cmdList.Begin(0u);
        var staging = device.ScheduleTextureUpload(texture, bake.Coverage, cmdList);
        cmdList.End();
        cmdList.ExecuteOn(device);
        device.WaitForIdle();
        staging.Dispose();

        return new D3D12FontAtlas(bake, texture, srvHeap, srvGpu);
    }
}
