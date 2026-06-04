using System;
using Opus.Engine.Input;
using Opus.Engine.Pal.Application;
using Silk.NET.SDL;

namespace Opus.Engine.Pal.Sdl3;

/// <summary>
/// SDL-backed <see cref="IWindowService"/> implementation. Per ADR-0023 the long-term
/// target is SDL3; the current Silk.NET 2.x ships SDL2 bindings. The class name omits the
/// version digit so the migration to <c>Silk.NET 3.x</c> + SDL3 is internal.
///
/// Lifecycle:
/// <list type="number">
/// <item><description><see cref="Open"/> initialises SDL_VIDEO + creates the window.</description></item>
/// <item><description><see cref="PollEvents"/> drains the event queue once per frame.</description></item>
/// <item><description><see cref="Close"/> destroys the window + tears SDL_VIDEO back down.</description></item>
/// </list>
///
/// Implements <see cref="INativeWindowAccess"/> so the D3D12 / Vulkan / Metal swap chain
/// constructors can fish out the OS window pointer without leaking SDL types.
/// </summary>
public sealed unsafe partial class SdlWindowService : IWindowService, INativeWindowAccess
{
    private readonly Sdl _sdl;
    private Window* _window;
    private string _title = string.Empty;
    private bool _disposed;

    public SdlWindowService()
    {
        _sdl = Sdl.GetApi();
    }

    public bool IsOpen => _window != null;

    public (int Width, int Height) Size
    {
        get
        {
            if (_window == null)
            {
                return (0, 0);
            }

            int w, h;
            _sdl.GetWindowSize(_window, &w, &h);
            return (w, h);
        }
    }

    /// <summary>Absolute mouse cursor position in window-local pixel space (origin top-left).
    /// Polls <c>SDL_GetMouseState</c> directly so callers get an accurate value even when
    /// the cursor warped or moved while events were not being drained. Returns
    /// <c>(0, 0)</c> when the window is closed.</summary>
    public (int X, int Y) GetMousePosition()
    {
        if (_window == null)
        {
            return (0, 0);
        }

        int x, y;
        _sdl.GetMouseState(&x, &y);
        return (x, y);
    }

    public void SetRelativeMouseMode(bool enabled)
    {
        if (_window == null)
        {
            return;
        }

        if (_sdl.SetRelativeMouseMode((SdlBool)(enabled ? 1 : 0)) < 0)
        {
            ThrowSdlError("SDL_SetRelativeMouseMode");
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            _title = value ?? string.Empty;
            if (_window != null)
            {
                _sdl.SetWindowTitle(_window, _title);
            }
        }
    }

    public event Action? Opened;

    public event Action? CloseRequested;

    public event Action<int, int>? Resized;

    /// <summary>Raised on every SDL <c>KEYDOWN</c> event (including OS auto-repeats — the
    /// consumer is expected to filter if it cares). Unknown keys map to <see cref="Key.None"/>
    /// and the event is suppressed so handlers see only engine-recognised codes.</summary>
    public event Action<Key>? KeyPressed;

    /// <summary>Raised on every SDL <c>KEYUP</c> event. Same mapping rules as
    /// <see cref="KeyPressed"/>.</summary>
    public event Action<Key>? KeyReleased;

    /// <summary>Raised on every SDL <c>MOUSEWHEEL</c> event with the vertical wheel delta
    /// (positive = scroll up / forward, negative = scroll down / back). Horizontal wheel
    /// motion is dropped at this milestone — the Garage / Match screens have no use for it
    /// yet.</summary>
    public event Action<float>? MouseWheelScrolled;

    /// <summary>Raised on every SDL <c>MOUSEBUTTONDOWN</c> event for a button the engine
    /// recognises (Left / Right / Middle). Other buttons are dropped silently.</summary>
    public event Action<MouseButton>? MouseButtonPressed;

    /// <summary>Raised on every SDL <c>MOUSEBUTTONUP</c> event. Same mapping rules as
    /// <see cref="MouseButtonPressed"/>.</summary>
    public event Action<MouseButton>? MouseButtonReleased;

    /// <summary>Raised on every SDL <c>MOUSEMOTION</c> event with the per-event relative
    /// motion (Δx, Δy in pixels). Absolute position is not exposed — handlers that need it
    /// poll <c>SDL_GetMouseState</c> or accumulate the relative deltas.</summary>
    public event Action<int, int>? MouseMoved;

    public void Open(WindowOptions options)
    {
        if (_window != null)
        {
            return;
        }

        if (_sdl.Init(Sdl.InitVideo) < 0)
        {
            ThrowSdlError("SDL_Init(VIDEO)");
        }

        var flags = (uint)WindowFlags.Shown;
        if (options.Resizable)
        {
            flags |= (uint)WindowFlags.Resizable;
        }

        flags |= options.Mode switch
        {
            WindowMode.BorderlessFullscreen => (uint)WindowFlags.FullscreenDesktop,
            WindowMode.ExclusiveFullscreen => (uint)WindowFlags.Fullscreen,
            _ => 0u,
        };

        _title = options.Title;
        _window = _sdl.CreateWindow(
            _title,
            Sdl.WindowposCentered,
            Sdl.WindowposCentered,
            options.Width,
            options.Height,
            flags);

        if (_window == null)
        {
            ThrowSdlError("SDL_CreateWindow");
        }

        Opened?.Invoke();
    }

    public void Close()
    {
        if (_window == null)
        {
            return;
        }

        CloseRequested?.Invoke();

        _sdl.DestroyWindow(_window);
        _window = null;
        _sdl.QuitSubSystem(Sdl.InitVideo);
    }

    public NativeWindowHandle GetNativeHandle()
    {
        if (_window == null)
        {
            throw new InvalidOperationException("Window must be open before requesting the native handle.");
        }

        SysWMInfo info = default;
        _sdl.GetVersion(&info.Version);
        if (!(bool)_sdl.GetWindowWMInfo(_window, &info))
        {
            ThrowSdlError("SDL_GetWindowWMInfo");
        }

        if (OperatingSystem.IsWindows())
        {
            return NativeWindowHandle.Win32((IntPtr)info.Info.Win.Hwnd);
        }

        if (OperatingSystem.IsMacOS())
        {
            return NativeWindowHandle.Cocoa((IntPtr)info.Info.Cocoa.Window);
        }

        if (OperatingSystem.IsLinux())
        {
            // Wayland support is deferred to Phase R-1+ Linux work — Silk.NET's SysWMInfo
            // shape varies by SDL version. X11 is the lowest-common-denominator path.
            return NativeWindowHandle.X11(
                (IntPtr)info.Info.X11.Window,
                (IntPtr)info.Info.X11.Display);
        }

        return new NativeWindowHandle(NativeWindowKind.Unknown, IntPtr.Zero, IntPtr.Zero);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Close() already QuitSubSystem'd VIDEO; calling _sdl.Quit() here would tear down
        // SDL's global session. Don't Dispose the Sdl API either — it owns the native DLL
        // handle, and repeatedly load/unload-ing it via xUnit's per-test create/destroy
        // cycle eventually exhausts Windows loader resources (testhost hangs after ~12
        // D3D12 integration tests in a single run).
        Close();
        _disposed = true;
    }

    private void ThrowSdlError(string operation)
    {
        var error = _sdl.GetErrorS();
        throw new InvalidOperationException($"{operation} failed: {error}");
    }
}
