using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;
using Opus.Engine.Ui;
using Opus.Engine.Ui.Direct3D12.Batching;
using Opus.Engine.Ui.Direct3D12.Text;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Opus.Engine.Ui.Direct3D12;

/// <summary>
/// Direct3D 12 implementation of <see cref="IDrawSurface"/>. Primitives accumulate in a
/// CPU-side quad batch during a frame; <see cref="EndFrame"/> uploads the batch into the
/// current frame's vertex buffer and issues a single non-indexed draw against the bound
/// render target.
/// <para>
/// Caller orchestration (the game client host, when it lands):
/// open the command list → transition swap-chain back-buffer Present→RenderTarget → call
/// <see cref="BeginFrame"/> → push <see cref="IDrawSurface"/> primitives → call
/// <see cref="EndFrame"/> → transition RenderTarget→Present → close + execute + Present.
/// The surface does not own the command list, swap chain, or render target — those belong
/// to the host. It does own the root sig / PSO / per-frame vertex buffers / quad batch.
/// </para>
/// </summary>
public sealed unsafe class D3D12DrawSurface : IDrawSurface, IDisposable
{
    private const uint RootParamViewport = 0u;
    private const uint RootParamAtlas = 1u;
    private const uint ViewportRootDwords = 2u;
    private const int DefaultMaxQuadsPerFrame = 4096;
    private const string PipelineDebugName = "ui-sprite";

    private static readonly int VertexStrideBytes = Marshal.SizeOf<UiQuadVertex>();
    private static readonly int VerticesPerQuad = 6;

    private readonly D3D12RhiDevice _device;
    private readonly D3D12FontAtlas _atlas;
    private readonly D3D12RootSignature _rootSig;
    private readonly D3D12GraphicsPipeline _pso;
    private readonly D3D12Buffer[] _vertexBuffers;
    private readonly UiQuadBatch _batch;
    private readonly List<BatchSegment> _segments = new();
    private readonly int _maxQuads;

    private D3D12CommandList? _activeList;
    private CpuDescriptorHandle _activeRtv;
    private int _activeSlot;
    private int _segmentStartVertex;
    private GpuDescriptorHandle _segmentSrvTable;
    private IntPtr _segmentSrvHeap;
    private bool _frameOpen;
    private bool _disposed;

    private D3D12DrawSurface(
        D3D12RhiDevice device,
        D3D12FontAtlas atlas,
        D3D12RootSignature rootSig,
        D3D12GraphicsPipeline pso,
        D3D12Buffer[] vertexBuffers,
        int maxQuads)
    {
        _device = device;
        _atlas = atlas;
        _rootSig = rootSig;
        _pso = pso;
        _vertexBuffers = vertexBuffers;
        _maxQuads = maxQuads;
        _batch = new UiQuadBatch(maxQuads);
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>Compiles the sprite shaders, builds the PSO, allocates per-slot vertex
    /// buffers. <paramref name="frameSlots"/> matches the host's swap-chain back-buffer
    /// count so one VB rides each in-flight frame without racing GPU consumption.</summary>
    public static D3D12DrawSurface Create(
        D3D12RhiDevice device,
        D3D12FontAtlas atlas,
        D3D12ShaderCompiler compiler,
        Format renderTargetFormat,
        int frameSlots = D3D12SwapChain.BufferCount,
        int maxQuadsPerFrame = DefaultMaxQuadsPerFrame)
    {
        if (frameSlots < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(frameSlots), "frameSlots must be >= 1.");
        }

        if (maxQuadsPerFrame < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxQuadsPerFrame), "maxQuadsPerFrame must be >= 1.");
        }

        var rootSig = D3D12RootSignatureFactory.CreateUiSprite(device);
        var vsBytecode = compiler.Compile(UiSpriteShaders.VertexShader, "main", "vs_6_0", "ui-sprite.vs.hlsl");
        var psBytecode = compiler.Compile(UiSpriteShaders.PixelShader, "main", "ps_6_0", "ui-sprite.ps.hlsl");
        var pso = D3D12GraphicsPipelineFactory.CreateUiSprite(device, rootSig, vsBytecode, psBytecode, renderTargetFormat);

        var bytesPerSlot = maxQuadsPerFrame * VerticesPerQuad * VertexStrideBytes;
        var vbs = new D3D12Buffer[frameSlots];
        for (var i = 0; i < frameSlots; i++)
        {
            vbs[i] = device.CreateGraphicsBuffer(new RhiBufferDescription(
                $"{PipelineDebugName}.vb.{i}", bytesPerSlot, RhiBufferUsage.Vertex));
        }

        return new D3D12DrawSurface(device, atlas, rootSig, pso, vbs, maxQuadsPerFrame);
    }

    /// <summary>Latches the host-supplied command list + RTV + frame slot for one frame and
    /// pins viewport + scissor. Resets the per-frame quad batch.</summary>
    public void BeginFrame(D3D12CommandList commandList, CpuDescriptorHandle renderTarget, int frameSlot, int viewportPxWidth, int viewportPxHeight)
    {
        ArgumentNullException.ThrowIfNull(commandList);
        if (_frameOpen)
        {
            throw new InvalidOperationException("BeginFrame called twice without an intervening EndFrame.");
        }

        if (frameSlot < 0 || frameSlot >= _vertexBuffers.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(frameSlot), $"frameSlot {frameSlot} outside [0, {_vertexBuffers.Length}).");
        }

        if (viewportPxWidth <= 0 || viewportPxHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportPxWidth), "Viewport dimensions must be > 0.");
        }

        _activeList = commandList;
        _activeRtv = renderTarget;
        _activeSlot = frameSlot;
        Width = viewportPxWidth;
        Height = viewportPxHeight;

        commandList.OMSetRenderTarget(renderTarget);
        commandList.RSSetViewport(viewportPxWidth, viewportPxHeight);
        commandList.RSSetScissorRect(viewportPxWidth, viewportPxHeight);
        _batch.Clear();
        _segments.Clear();
        OpenAtlasSegment();
        _frameOpen = true;
    }

    /// <summary>Uploads the accumulated batch, records the per-segment draws, releases the
    /// latched command list. Empty frames are legal — they only pay the cost of state setup.
    /// Multiple segments arise when <see cref="DrawTexturedRect"/> swaps the bound SRV
    /// mid-batch; each segment is a contiguous vertex range sharing one SRV table.</summary>
    public void EndFrame()
    {
        if (!_frameOpen)
        {
            throw new InvalidOperationException("EndFrame called without a matching BeginFrame.");
        }

        FinalizeSegment();
        if (_segments.Count > 0)
        {
            FlushSegments();
        }

        _activeList = null;
        _frameOpen = false;
    }

    public void Clear(Color color)
    {
        EnsureFrameOpen();
        _batch.Clear();
        _segments.Clear();
        OpenAtlasSegment();
        var list = _activeList!;
        list.ClearRenderTargetView(_activeRtv, color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
    }

    public void FillRect(int x, int y, int w, int h, Color color)
    {
        EnsureFrameOpen();
        UiQuadGeometry.Rect(_batch, x, y, w, h, color, _atlas.WhiteUv);
    }

    public void StrokeRect(int x, int y, int w, int h, int thickness, Color color)
    {
        EnsureFrameOpen();
        if (thickness <= 0 || w <= 0 || h <= 0)
        {
            return;
        }

        var inset = Math.Min(thickness, h / 2);
        var sideHeight = Math.Max(0, h - (2 * inset));
        var whiteUv = _atlas.WhiteUv;
        UiQuadGeometry.Rect(_batch, x, y, w, thickness, color, whiteUv);
        UiQuadGeometry.Rect(_batch, x, y + h - thickness, w, thickness, color, whiteUv);
        UiQuadGeometry.Rect(_batch, x, y + thickness, thickness, sideHeight, color, whiteUv);
        UiQuadGeometry.Rect(_batch, x + w - thickness, y + thickness, thickness, sideHeight, color, whiteUv);
    }

    public void DrawLine(int x0, int y0, int x1, int y1, int thickness, Color color)
    {
        EnsureFrameOpen();
        UiQuadGeometry.Line(_batch, x0, y0, x1, y1, thickness, color, _atlas.WhiteUv);
    }

    public void FillCircle(int cx, int cy, int radius, Color color)
    {
        EnsureFrameOpen();
        UiQuadGeometry.Circle(_batch, cx, cy, radius, color);
    }

    public void StrokeCircle(int cx, int cy, int radius, int thickness, Color color)
    {
        EnsureFrameOpen();
        UiQuadGeometry.Ring(_batch, cx, cy, radius, thickness, color);
    }

    public void DrawText(string text, int x, int y, int fontSize, Color color)
    {
        EnsureFrameOpen();
        UiTextLayout.Append(_batch, text, x, y, fontSize, color, _atlas.Bake);
    }

    public int MeasureText(string text, int fontSize) =>
        UiTextLayout.Measure(text, fontSize, _atlas.Bake);

    /// <summary>Draws one freehand ink stroke — a polyline swept to the stroke width with round
    /// caps and joins (see ADR-0033). Expands into the atlas batch segment alongside the other
    /// solid-colour primitives, so it needs no SRV swap. A single-point stroke renders as a dot;
    /// an empty or zero-width stroke draws nothing.</summary>
    public void DrawInkStroke(InkStroke stroke)
    {
        EnsureFrameOpen();
        ArgumentNullException.ThrowIfNull(stroke);
        UiInkTessellator.Append(_batch, stroke, _atlas.WhiteUv);
    }

    /// <summary>Draws every committed mark of a hand-drawn <see cref="InkAnnotationLayer"/> oldest
    /// first, then the in-progress stroke (when one is being drawn) on top as a live preview. The
    /// persistent-marks + undo/redo state lives in the layer; this only renders its current
    /// contents each frame.</summary>
    public void DrawAnnotations(InkAnnotationLayer annotations)
    {
        EnsureFrameOpen();
        ArgumentNullException.ThrowIfNull(annotations);

        var whiteUv = _atlas.WhiteUv;
        var committed = annotations.CommittedStrokes;
        for (var i = 0; i < committed.Count; i++)
        {
            UiInkTessellator.Append(_batch, committed[i], whiteUv);
        }

        if (annotations.InProgressStroke is { } preview)
        {
            UiInkTessellator.Append(_batch, preview, whiteUv);
        }
    }

    /// <summary>Composites an externally-rendered RGBA texture into the current frame as a
    /// rectangular quad. The supplied <paramref name="srvHeap"/> is bound for the draw and
    /// the existing atlas batch flushes before / after — chrome quads queued before the
    /// call render with the atlas SRV, chrome quads queued after re-bind the atlas as the
    /// next segment opens. Use this to drop an offscreen scene viewport into the UI batch.</summary>
    public void DrawTexturedRect(GpuDescriptorHandle srvTable, ID3D12DescriptorHeap* srvHeap, int x, int y, int w, int h)
        => DrawTexturedRect(srvTable, srvHeap, x, y, w, h, Color.White);

    /// <summary>As <see cref="DrawTexturedRect(GpuDescriptorHandle, ID3D12DescriptorHeap*, int, int, int, int)"/>,
    /// with an RGBA multiplier for fading or tinting the external sprite.</summary>
    public void DrawTexturedRect(
        GpuDescriptorHandle srvTable,
        ID3D12DescriptorHeap* srvHeap,
        int x,
        int y,
        int w,
        int h,
        Color tint)
    {
        EnsureFrameOpen();
        if (srvHeap == null)
        {
            throw new ArgumentNullException(nameof(srvHeap));
        }

        if (w <= 0 || h <= 0)
        {
            return;
        }

        FinalizeSegment();
        _segmentStartVertex = _batch.VertexCount;
        _segmentSrvTable = srvTable;
        _segmentSrvHeap = (IntPtr)srvHeap;
        UiQuadGeometry.TexturedRectRgba(_batch, x, y, w, h, tint);
        FinalizeSegment();
        OpenAtlasSegment();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        for (var i = 0; i < _vertexBuffers.Length; i++)
        {
            _vertexBuffers[i].Dispose();
        }

        _pso.Dispose();
        _rootSig.Dispose();
    }

    private void FlushSegments()
    {
        if (_batch.QuadCount > _maxQuads)
        {
            throw new InvalidOperationException(
                $"UI quad batch ({_batch.QuadCount}) exceeds capacity ({_maxQuads}); raise maxQuadsPerFrame.");
        }

        var vertexBuffer = _vertexBuffers[_activeSlot];
        vertexBuffer.Upload(MemoryMarshal.AsBytes(_batch.Vertices));

        var viewportConstants = new ViewportConstants { Width = Width, Height = Height };
        var list = _activeList!;
        list.SetGraphicsRootSignature(_rootSig);
        list.SetPipelineState(_pso);
        list.SetGraphicsRoot32BitConstants(RootParamViewport, ViewportRootDwords, in viewportConstants);
        list.IASetTriangleListTopology();
        list.IASetVertexBuffer(vertexBuffer, (uint)VertexStrideBytes);

        foreach (var seg in _segments)
        {
            list.SetDescriptorHeaps((ID3D12DescriptorHeap*)seg.SrvHeap);
            list.SetGraphicsRootDescriptorTable(RootParamAtlas, seg.SrvTable);
            list.DrawInstanced((uint)seg.VertexCount, 1u, (uint)seg.StartVertex, 0u);
        }
    }

    private void OpenAtlasSegment()
    {
        _segmentStartVertex = _batch.VertexCount;
        _segmentSrvTable = _atlas.SrvGpu;
        _segmentSrvHeap = (IntPtr)_atlas.SrvHeap;
    }

    private void FinalizeSegment()
    {
        var count = _batch.VertexCount - _segmentStartVertex;
        if (count <= 0)
        {
            return;
        }

        _segments.Add(new BatchSegment(_segmentStartVertex, count, _segmentSrvTable, _segmentSrvHeap));
    }

    private void EnsureFrameOpen()
    {
        if (!_frameOpen)
        {
            throw new InvalidOperationException("Draw call outside BeginFrame/EndFrame bracket.");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ViewportConstants
    {
        public float Width;
        public float Height;
    }

    private readonly record struct BatchSegment(int StartVertex, int VertexCount, GpuDescriptorHandle SrvTable, IntPtr SrvHeap);
}
