using System;
using System.Collections.Generic;
using FluentAssertions;
using Opus.Engine.Direct3D12.Tests.Fixtures;
using Opus.Engine.Pal.Application;
using Opus.Engine.Pal.Windows.Direct3D12;
using Xunit;

namespace Opus.Engine.Direct3D12.Tests.Pal;

public sealed class D3D12WindowSessionSmokeTests
{
    [SkippableFact]
    public void Window_session_presents_two_cleared_frames()
    {
        using var host = D3D12SmokeHost.OpenWindow("opus-d3d12-window-smoke");
        using var frameLoop = new D3D12UiFrameLoop(host.Session);

        for (var i = 0; i < 2; i++)
        {
            host.Session.Window.PollEvents();
            var frame = frameLoop.BeginFrame();
            frame.CommandList.OMSetRenderTarget(frame.RenderTargetView);
            frame.CommandList.ClearRenderTargetView(frame.RenderTargetView, 0.05f * i, 0.1f, 0.2f, 1f);
            frameLoop.EndFrame();
        }

        frameLoop.IsFrameOpen.Should().BeFalse();
        host.Session.SwapChain.Width.Should().Be(192);
        host.Session.SwapChain.Height.Should().Be(128);
    }

    [Fact]
    public void Resize_bridge_forwards_and_detaches_window_resize_events()
    {
        var window = new FakeWindowService();
        var forwarded = new List<(int Width, int Height)>();

        using (new D3D12WindowResizeBridge(window, (w, h) => forwarded.Add((w, h))))
        {
            window.RaiseResized(1280, 720);
            window.RaiseResized(0, 0);
        }

        window.RaiseResized(1920, 1080);

        forwarded.Should().Equal((1280, 720), (0, 0));
    }

    private sealed class FakeWindowService : IWindowService
    {
        public bool IsOpen { get; private set; }

        public (int Width, int Height) Size { get; private set; }

        public string Title { get; set; } = "fake";

        public event Action? Opened;

        public event Action? CloseRequested;

        public event Action<int, int>? Resized;

        public void Open(WindowOptions options)
        {
            IsOpen = true;
            Size = (options.Width, options.Height);
            Opened?.Invoke();
        }

        public void PollEvents()
        {
        }

        public void Close()
        {
            IsOpen = false;
            CloseRequested?.Invoke();
        }

        public void Dispose()
        {
        }

        public void RaiseResized(int width, int height)
        {
            Size = (width, height);
            Resized?.Invoke(width, height);
        }
    }
}
