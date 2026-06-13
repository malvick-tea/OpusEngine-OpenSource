using System;
using System.Globalization;
using Opus.Editor.Core;

namespace Opus.App.Editor.Cli;

/// <summary>
/// Pure, total readers over a raw CLI argument vector: find an option's value, test a flag, and parse the
/// editor's value shapes (a comma-separated <see cref="Float3"/>, a float, an optional float / Float3 / cone,
/// a light kind). Each is side-effect-free and never throws — a present-but-malformed value returns false so
/// the per-command parser can name which option was wrong. Shared by every command parser so option scanning
/// lives in one place.
/// </summary>
internal static class CliArgumentReader
{
    /// <summary>The value following <paramref name="option"/> in the argument vector, or null when the
    /// option is absent or is the final token (no value follows). Scans from index 1 (past the verb).</summary>
    public static string? FindOptionValue(string[] args, string option)
    {
        for (int i = 1; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], option, StringComparison.Ordinal))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    /// <summary>True when <paramref name="flag"/> is present anywhere past the verb.</summary>
    public static bool HasFlag(string[] args, string flag)
    {
        for (int i = 1; i < args.Length; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Parses three comma-separated invariant floats into a <see cref="Float3"/>.</summary>
    public static bool TryParseFloat3(string text, out Float3 value)
    {
        value = Float3.Zero;
        string[] parts = text.Split(',');
        if (parts.Length != 3)
        {
            return false;
        }

        if (!TryFloat(parts[0], out float x) || !TryFloat(parts[1], out float y) || !TryFloat(parts[2], out float z))
        {
            return false;
        }

        value = new Float3(x, y, z);
        return true;
    }

    public static bool TryParseLightKind(string text, out SceneLightKind kind)
    {
        switch (text.ToLowerInvariant())
        {
            case "directional":
                kind = SceneLightKind.Directional;
                return true;
            case "point":
                kind = SceneLightKind.Point;
                return true;
            case "spot":
                kind = SceneLightKind.Spot;
                return true;
            default:
                kind = SceneLightKind.Directional;
                return false;
        }
    }

    /// <summary>Reads a float option with a fallback when the option is absent; false only when present and
    /// malformed.</summary>
    public static bool TryParseFloatOption(string[] args, string option, float fallback, out float value)
    {
        value = fallback;
        string? text = FindOptionValue(args, option);
        return text is null || TryFloat(text, out value);
    }

    /// <summary>Reads an optional <c>Float3</c> option. Absent is success (null); present-but-malformed is
    /// the only failure, so the caller can report which option was wrong.</summary>
    public static bool TryParseOptionalFloat3(string[] args, string option, out Float3? value)
    {
        value = null;
        string? text = FindOptionValue(args, option);
        if (text is null)
        {
            return true;
        }

        if (!TryParseFloat3(text, out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    /// <summary>Reads an optional float option. Absent is success (null); present-but-malformed fails.</summary>
    public static bool TryParseOptionalFloat(string[] args, string option, out float? value)
    {
        value = null;
        string? text = FindOptionValue(args, option);
        if (text is null)
        {
            return true;
        }

        if (!TryFloat(text, out float parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    /// <summary>Reads an optional two-value cone option (<c>inner,outer</c> degrees). Absent is success
    /// (both null); present-but-malformed fails.</summary>
    public static bool TryParseOptionalCone(string[] args, string option, out float? inner, out float? outer)
    {
        inner = null;
        outer = null;
        string? text = FindOptionValue(args, option);
        if (text is null)
        {
            return true;
        }

        string[] parts = text.Split(',');
        if (parts.Length != 2 || !TryFloat(parts[0], out float parsedInner) || !TryFloat(parts[1], out float parsedOuter))
        {
            return false;
        }

        inner = parsedInner;
        outer = parsedOuter;
        return true;
    }

    /// <summary>A <c>--loop</c> option defaults to true; only an explicit "false" turns looping off.</summary>
    public static bool ParseLoopOption(string[] args)
    {
        string? value = FindOptionValue(args, CliOptions.LoopOption);
        return value is null || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryFloat(string text, out float value) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
