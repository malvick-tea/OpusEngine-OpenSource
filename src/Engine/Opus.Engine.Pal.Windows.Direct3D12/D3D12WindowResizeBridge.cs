using System;
using Opus.Engine.Pal.Application;

namespace Opus.Engine.Pal.Windows.Direct3D12;

/// <summary>
/// Keeps a D3D12 host's render surface sized to its window: subscribes to
/// <see cref="IWindowService.Resized"/> and forwards every new dimension pair to a
/// caller-supplied handler — in practice
/// <see cref="Opus.Engine.Rhi.Direct3D12.D3D12SwapChain.Resize"/>.
///
/// <para>A D3D12 swap chain is created at a fixed size and does not track its window on
/// its own — unlike Raylib, which rebuilds its framebuffer implicitly. A resizable D3D12
/// host therefore needs this explicit bridge, or the back buffers desynchronise from the
/// client area on the first drag-resize.</para>
///
/// <para>Resize events arrive on the thread that pumps the window's event queue — the
/// device-owning main thread — so the swap-chain rebuild the handler triggers runs where
/// D3D12 requires it. The handler is expected to no-op a degenerate or unchanged size
/// (<see cref="Opus.Engine.Rhi.Direct3D12.D3D12SwapChain.Resize"/> already does), so
/// this bridge stays a thin, debounce-free forwarder.</para>
///
/// <para><see cref="Dispose"/> detaches the subscription. Construct one per host session
/// and dispose it before the window so a late event cannot reach a torn-down surface.</para>
/// </summary>
public sealed class D3D12WindowResizeBridge : IDisposable
{
    private readonly IWindowService _window;
    private readonly Action<int, int> _onResized;
    private bool _disposed;

    public D3D12WindowResizeBridge(IWindowService window, Action<int, int> onResized)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(onResized);

        _window = window;
        _onResized = onResized;
        _window.Resized += OnWindowResized;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _window.Resized -= OnWindowResized;
        _disposed = true;
    }

    private void OnWindowResized(int width, int height) => _onResized(width, height);
}
