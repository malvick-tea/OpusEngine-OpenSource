using System;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Lifecycle bundle for an offscreen scene colour target — a single
/// <see cref="RhiTextureUsage.ColorTarget"/> + <see cref="RhiTextureUsage.Sampled"/>
/// texture plus its private RTV heap and a shader-visible SRV heap that the UI batch
/// binds when compositing the rendered frame as a quad.
/// <para>
/// Persistent across frames — created once at viewport construction, reused every frame.
/// The texture rests in <see cref="ResourceStates.PixelShaderResource"/> between frames
/// so the UI sampler reads a coherent image; the scene pass transitions in and back out
/// of <see cref="ResourceStates.RenderTarget"/> internally via the frame graph.
/// </para></summary>
public sealed unsafe class SceneViewportTarget : IDisposable
{
    /// <summary>Eight-bit-per-channel LDR target — matches the swap chain's default colour
    /// format and what <see cref="TonemapPass"/>'s PSO expects to write into.</summary>
    public const Format DefaultFormat = Format.FormatR8G8B8A8Unorm;

    private readonly ID3D12DescriptorHeap* _rtvHeap;
    private readonly ID3D12DescriptorHeap* _srvHeap;
    private bool _disposed;

    public SceneViewportTarget(D3D12RhiDevice device, int width, int height, string namePrefix = "scene-viewport")
    {
        ArgumentNullException.ThrowIfNull(device);
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "width must be > 0.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "height must be > 0.");
        }

        Width = width;
        Height = height;
        Color = device.CreateGraphicsTexture(new RhiTextureDescription(
            $"{namePrefix}.color", width, height, 1,
            RhiTextureFormat.Rgba8Unorm, RhiTextureUsage.ColorTarget | RhiTextureUsage.Sampled));

        _rtvHeap = device.CreateRtvDescriptorHeap(1u);
        RtvHandle = device.CreateRenderTargetView(Color, _rtvHeap, slotIndex: 0u);

        _srvHeap = device.CreateSrvDescriptorHeap(1u);
        SrvTable = device.CreateShaderResourceView(Color, _srvHeap, slotIndex: 0u);
    }

    public int Width { get; }

    public int Height { get; }

    public Format Format => DefaultFormat;

    public D3D12Texture Color { get; }

    public CpuDescriptorHandle RtvHandle { get; }

    /// <summary>Shader-visible heap holding the single SRV that points at <see cref="Color"/>.
    /// Bound by the UI draw surface before the textured composite quad.</summary>
    public ID3D12DescriptorHeap* SrvHeap => _srvHeap;

    public GpuDescriptorHandle SrvTable { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _srvHeap->Release();
        _rtvHeap->Release();
        Color.Dispose();
        _disposed = true;
    }
}
