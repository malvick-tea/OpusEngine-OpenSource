using System;
using System.Collections.Generic;
using Opus.Engine.Input;

namespace Opus.Editor.Ui;

/// <summary>
/// Maps the engine's key set onto the characters a rename buffer accepts: the letters (lowercase — the
/// input layer carries no shift state), the top-row digits, space, period, and hyphen. The input layer is a
/// per-frame key snapshot rather than a text-input event stream, so this is deliberately the small safe
/// charset for element names, not a general text engine. Pure.
/// </summary>
public static class RenameKeyMap
{
    private static readonly (Key Key, char Character)[] Map = BuildMap();

    /// <summary>The characters whose keys were freshly pressed this frame, in a stable order.</summary>
    public static IReadOnlyList<char> PressedCharacters(IInputSource input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var pressed = new List<char>();
        foreach (var (key, character) in Map)
        {
            if (input.IsKeyPressed(key))
            {
                pressed.Add(character);
            }
        }

        return pressed;
    }

    private static (Key Key, char Character)[] BuildMap()
    {
        var map = new List<(Key, char)>();
        for (var key = Key.A; key <= Key.Z; key++)
        {
            map.Add((key, (char)('a' + (key - Key.A))));
        }

        for (var key = Key.D0; key <= Key.D9; key++)
        {
            map.Add((key, (char)('0' + (key - Key.D0))));
        }

        map.Add((Key.Space, ' '));
        map.Add((Key.Period, '.'));
        map.Add((Key.Hyphen, '-'));
        return map.ToArray();
    }
}
