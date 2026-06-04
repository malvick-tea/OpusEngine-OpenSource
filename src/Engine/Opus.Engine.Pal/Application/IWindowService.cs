using System;

namespace Opus.Engine.Pal.Application;

/// <summary>
/// Owns the host's main window. Created by Engine.Pal.{platform}; consumed by Engine.Renderer
/// (for the swap-chain) and Client.Core (for the boot loop). Single instance per process —
/// multi-window is out of scope until M9.
/// </summary>
public interface IWindowService : IDisposable
{
    /// <summary>True after <see cref="Open"/> returns and before <see cref="Close"/>.</summary>
    bool IsOpen { get; }

    /// <summary>Current pixel dimensions (may differ from requested on HiDPI / mobile).</summary>
    (int Width, int Height) Size { get; }

    /// <summary>Title bar text. Setter is a no-op on platforms without title bars.</summary>
    string Title { get; set; }

    /// <summary>Fired once the OS has reported the window is ready to render.</summary>
    event Action? Opened;

    /// <summary>Fired when the user clicks the close button or the OS terminates the app.</summary>
    event Action? CloseRequested;

    /// <summary>Fired with the new size after a resize/orientation change.</summary>
    event Action<int, int>? Resized;

    /// <summary>Creates the window with the given options. Idempotent — second call is a no-op.</summary>
    void Open(WindowOptions options);

    /// <summary>Pumps the OS message queue. Must be called once per frame from the main thread.</summary>
    void PollEvents();

    /// <summary>Asks the OS to close the window. Fires <see cref="CloseRequested"/> first.</summary>
    void Close();
}
