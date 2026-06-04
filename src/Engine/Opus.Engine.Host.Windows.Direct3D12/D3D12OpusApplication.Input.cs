using System;
using Opus.Engine.Host.Windows.Direct3D12.Diagnostics;
using Opus.Engine.Input;

namespace Opus.Engine.Host.Windows.Direct3D12;

public sealed unsafe partial class D3D12OpusApplication
{
    /// <summary>Hotkey that captures a metadata-tagged PNG screenshot of the live window. F9
    /// sits next to the F10 overlay toggle so a tester can hide the overlay and grab a clean
    /// frame without leaving the keyboard.</summary>
    private const Key ScreenshotHotkey = Key.F9;

    /// <summary>Subscribes the window hotkeys (overlay toggle + screenshot capture) to the
    /// SDL window's key events. The window pumps these synchronously from <c>OpusHost.Step</c>
    /// via the window-service adapter, so a press is handled before the same frame's
    /// <c>Render</c>. Called once at the end of construction, after the GPU rig is live.</summary>
    private void SubscribeWindowHotkeys()
    {
        _session.Window.KeyPressed += OnWindowKeyPressed;
        _session.Window.KeyReleased += OnWindowKeyReleased;
    }

    /// <summary>Detaches the window-hotkey handlers. Called from <see cref="Dispose"/> before
    /// the session (and its window) is torn down so no dangling delegate survives the host.</summary>
    private void UnsubscribeWindowHotkeys()
    {
        _session.Window.KeyPressed -= OnWindowKeyPressed;
        _session.Window.KeyReleased -= OnWindowKeyReleased;
    }

    private void OnWindowKeyPressed(Key key)
    {
        _overlay.HandleToggleKeyDown(D3D12OverlayToggleKeyMap.From(key));
        if (key == ScreenshotHotkey && _screenshotHotkey.TryTrigger())
        {
            RequestScreenshot(D3D12HotkeyScreenshotPath.Build(_screenshotsDirectory, DateTimeOffset.UtcNow));
        }
    }

    private void OnWindowKeyReleased(Key key)
    {
        _overlay.HandleToggleKeyUp(D3D12OverlayToggleKeyMap.From(key));
        if (key == ScreenshotHotkey)
        {
            _screenshotHotkey.Release();
        }
    }
}
