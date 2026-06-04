using System;
using Opus.Engine.Pal.Sdl3;
using Opus.Engine.Rhi.Direct3D12;

namespace Opus.Engine.Pal.Windows.Direct3D12;

/// <summary>
/// Disposable composition of the four objects required to drive a live D3D12 frame loop
/// from a Windows process: an SDL3 window, a D3D12 RHI device, a flip-discard swap chain
/// matched to the window, and a DXC shader compiler.
///
/// <para>Lifted out of <c>the sample garage demo</c> in Phase B of the D3D12 migration so
/// the future <c>the game client</c> host and the <see cref="D3D12UiFrameLoop"/>
/// integration smoke tests compose the same path the demo runs.</para>
///
/// <para>Open-ness is best-effort: <see cref="TryOpen"/> returns <c>null</c> when no
/// D3D12-capable adapter is available so callers (smoke tests, demo) can skip cleanly
/// instead of throwing through the boot path.</para>
/// </summary>
public sealed class D3D12WindowSession : IDisposable
{
    private readonly SdlWindowService _window;
    private readonly D3D12RhiDevice _device;
    private readonly D3D12SwapChain _swapChain;
    private readonly D3D12ShaderCompiler _compiler;
    private bool _disposed;

    private D3D12WindowSession(
        SdlWindowService window,
        D3D12RhiDevice device,
        D3D12SwapChain swapChain,
        D3D12ShaderCompiler compiler)
    {
        _window = window;
        _device = device;
        _swapChain = swapChain;
        _compiler = compiler;
    }

    public SdlWindowService Window => _window;

    public D3D12RhiDevice Device => _device;

    public D3D12SwapChain SwapChain => _swapChain;

    public D3D12ShaderCompiler Compiler => _compiler;

    /// <summary>Brings up window + device + swap chain + compiler in the exact order the
    /// runtime path needs. Returns <c>null</c> when SDL video or D3D12 adapter
    /// initialisation fails — that's a host-environment problem, not a programming error,
    /// so callers branch on it instead of catching exceptions.</summary>
    public static D3D12WindowSession? TryOpen(D3D12WindowSessionOptions options)
    {
        SdlWindowService? window = null;
        D3D12RhiDevice? device = null;
        D3D12ShaderCompiler? compiler = null;
        D3D12SwapChain? swapChain = null;
        try
        {
            window = new SdlWindowService();
            try
            {
                window.Open(options.Window);
            }
            catch (InvalidOperationException)
            {
                return null;
            }

            device = D3D12RhiDevice.TryCreate(options.EnableDebugLayer);
            if (device is null)
            {
                return null;
            }

            compiler = new D3D12ShaderCompiler();
            swapChain = device.CreateSwapChain(
                window.GetNativeHandle().Handle,
                options.Window.Width,
                options.Window.Height);

            var session = new D3D12WindowSession(window, device, swapChain, compiler);
            window = null;
            device = null;
            compiler = null;
            swapChain = null;
            return session;
        }
        finally
        {
            swapChain?.Dispose();
            compiler?.Dispose();
            device?.Dispose();
            window?.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _device.WaitForIdle();
        }
        catch
        {
            // Best-effort drain before tear-down — the device may already be gone in a
            // crash path and we still need to release the GPU resources we own.
        }

        _swapChain.Dispose();
        _compiler.Dispose();
        _device.Dispose();
        _window.Dispose();
        _disposed = true;
    }
}
