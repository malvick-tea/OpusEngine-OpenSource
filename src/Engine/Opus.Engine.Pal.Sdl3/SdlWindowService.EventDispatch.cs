using Opus.Engine.Input;
using Silk.NET.SDL;

namespace Opus.Engine.Pal.Sdl3;

/// <summary>SDL event-loop pump + dispatch surface. Drains the SDL queue once per frame
/// and translates each raw <see cref="Event"/> union member into the engine-typed event
/// (Key, MouseButton, mouse motion delta, window close request, resize). Unknown event
/// types are dropped silently — the upstream <see cref="SdlKeyMap"/> /
/// <see cref="SdlMouseButtonMap"/> handle the per-type filtering.</summary>
public sealed unsafe partial class SdlWindowService
{
    public void PollEvents()
    {
        Event ev;
        while (_sdl.PollEvent(&ev) != 0)
        {
            DispatchEvent(in ev);
        }
    }

    private void DispatchEvent(in Event ev)
    {
        // SDL event union — the discriminator is the first uint32 field.
        var type = (EventType)ev.Type;
        switch (type)
        {
            case EventType.Quit:
                CloseRequested?.Invoke();
                break;

            case EventType.Windowevent:
                DispatchWindowEvent(in ev);
                break;

            case EventType.Keydown:
                DispatchKey(in ev, isPressed: true);
                break;

            case EventType.Keyup:
                DispatchKey(in ev, isPressed: false);
                break;

            case EventType.Mousewheel:
                DispatchMouseWheel(in ev);
                break;

            case EventType.Mousebuttondown:
                DispatchMouseButton(in ev, isPressed: true);
                break;

            case EventType.Mousebuttonup:
                DispatchMouseButton(in ev, isPressed: false);
                break;

            case EventType.Mousemotion:
                MouseMoved?.Invoke(ev.Motion.Xrel, ev.Motion.Yrel);
                break;
        }
    }

    private void DispatchWindowEvent(in Event ev)
    {
        var windowEvent = (WindowEventID)ev.Window.Event;
        if (windowEvent == WindowEventID.Resized || windowEvent == WindowEventID.SizeChanged)
        {
            Resized?.Invoke(ev.Window.Data1, ev.Window.Data2);
        }
        else if (windowEvent == WindowEventID.Close)
        {
            CloseRequested?.Invoke();
        }
    }

    private void DispatchKey(in Event ev, bool isPressed)
    {
        var key = SdlKeyMap.From((KeyCode)ev.Key.Keysym.Sym);
        if (key == Key.None)
        {
            return;
        }

        if (isPressed)
        {
            KeyPressed?.Invoke(key);
        }
        else
        {
            KeyReleased?.Invoke(key);
        }
    }

    private void DispatchMouseWheel(in Event ev)
    {
        // Prefer the float-precise field; integer Y fallback covers SDL builds that don't
        // populate PreciseY (older drivers / non-Windows hosts).
        var precise = ev.Wheel.PreciseY;
        var delta = precise != 0f ? precise : ev.Wheel.Y;
        if (delta != 0f)
        {
            MouseWheelScrolled?.Invoke(delta);
        }
    }

    private void DispatchMouseButton(in Event ev, bool isPressed)
    {
        var button = SdlMouseButtonMap.From(ev.Button.Button);
        if (!button.HasValue)
        {
            return;
        }

        if (isPressed)
        {
            MouseButtonPressed?.Invoke(button.Value);
        }
        else
        {
            MouseButtonReleased?.Invoke(button.Value);
        }
    }
}
