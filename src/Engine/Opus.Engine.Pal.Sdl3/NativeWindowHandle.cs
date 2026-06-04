using System;

namespace Opus.Engine.Pal.Sdl3;

/// <summary>
/// Platform-tagged opaque native window handle. The renderer's swap chain creation
/// path needs the underlying OS window pointer (HWND on Windows, NSWindow on macOS,
/// xcb/wayland surface on Linux); this struct carries it without leaking
/// platform-specific types into <see cref="Application.IWindowService"/>.
///
/// Runtime renderer dispatches on <see cref="Kind"/> to call the right swap chain
/// constructor (DXGI for HWND, MetalLayer for macOS, VkSurface for Linux).
/// </summary>
public readonly record struct NativeWindowHandle(NativeWindowKind Kind, IntPtr Handle, IntPtr Display)
{
    /// <summary>Win32 HWND. <see cref="Display"/> unused (always zero).</summary>
    public static NativeWindowHandle Win32(IntPtr hwnd) => new(NativeWindowKind.Win32, hwnd, IntPtr.Zero);

    /// <summary>X11 window + display. Both must be non-zero.</summary>
    public static NativeWindowHandle X11(IntPtr window, IntPtr display) => new(NativeWindowKind.X11, window, display);

    /// <summary>Wayland surface + display.</summary>
    public static NativeWindowHandle Wayland(IntPtr surface, IntPtr display) => new(NativeWindowKind.Wayland, surface, display);

    /// <summary>Cocoa NSWindow pointer. <see cref="Display"/> unused.</summary>
    public static NativeWindowHandle Cocoa(IntPtr nsWindow) => new(NativeWindowKind.Cocoa, nsWindow, IntPtr.Zero);
}

public enum NativeWindowKind : byte
{
    Unknown = 0,
    Win32 = 1,
    X11 = 2,
    Wayland = 3,
    Cocoa = 4,
}

/// <summary>Optional capability — implementations that can hand out a native window
/// handle for swap chain creation.</summary>
public interface INativeWindowAccess
{
    NativeWindowHandle GetNativeHandle();
}
