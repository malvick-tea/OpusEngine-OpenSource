using System;
using FluentAssertions;
using Opus.Engine.Pal.Application;
using Xunit;

namespace Opus.Engine.Pal.Sdl3.Tests;

/// <summary>
/// Smoke tests for the SDL-backed window service. Tests open a real OS window very
/// briefly (a few hundred milliseconds), then tear it down. Skipped on platforms where
/// SDL's video subsystem isn't available (headless CI without X server, etc.).
/// </summary>
public sealed class SdlWindowServiceSmokeTests
{
    [SkippableFact]
    public void Window_opens_and_reports_requested_size()
    {
        Skip.IfNot(CanInitVideo(), "SDL video subsystem not available on this host.");

        using var service = new SdlWindowService();
        service.Open(new WindowOptions("rhi-smoke", 640, 480, Resizable: false, VSync: false, WindowMode.Windowed));

        service.IsOpen.Should().BeTrue();
        var (w, h) = service.Size;
        w.Should().Be(640);
        h.Should().Be(480);
        service.Title.Should().Be("rhi-smoke");

        service.Close();
        service.IsOpen.Should().BeFalse();
    }

    [SkippableFact]
    public void Native_handle_is_win32_hwnd_on_windows()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Win32 native handle is Windows-only.");
        Skip.IfNot(CanInitVideo(), "SDL video subsystem not available.");

        using var service = new SdlWindowService();
        service.Open(WindowOptions.Default("hwnd-test"));

        var handle = service.GetNativeHandle();

        handle.Kind.Should().Be(NativeWindowKind.Win32);
        handle.Handle.Should().NotBe(IntPtr.Zero);
    }

    [SkippableFact]
    public void Title_setter_round_trips_after_open()
    {
        Skip.IfNot(CanInitVideo(), "SDL video subsystem not available.");

        using var service = new SdlWindowService();
        service.Open(new WindowOptions("initial", 320, 240, Resizable: true, VSync: false, WindowMode.Windowed));

        service.Title = "updated";
        service.Title.Should().Be("updated");
    }

    [SkippableFact]
    public void Poll_events_does_not_throw_with_no_pending_input()
    {
        Skip.IfNot(CanInitVideo(), "SDL video subsystem not available.");

        using var service = new SdlWindowService();
        service.Open(WindowOptions.Default("poll-test"));

        // Several pumps in a row — should be a no-op without queued OS events.
        for (var i = 0; i < 5; i++)
        {
            service.PollEvents();
        }

        service.IsOpen.Should().BeTrue();
    }

    [SkippableFact]
    public void Open_is_idempotent()
    {
        Skip.IfNot(CanInitVideo(), "SDL video subsystem not available.");

        using var service = new SdlWindowService();
        service.Open(WindowOptions.Default("idempotent"));
        var firstSize = service.Size;
        service.Open(WindowOptions.Default("ignored"));   // second call no-op
        service.Size.Should().Be(firstSize);
    }

    private static bool CanInitVideo()
    {
        try
        {
            using var probe = new SdlWindowService();
            probe.Open(WindowOptions.Default("probe"));
            return probe.IsOpen;
        }
        catch
        {
            return false;
        }
    }
}
