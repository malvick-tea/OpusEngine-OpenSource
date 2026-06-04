using System;
using System.Globalization;

namespace Opus.Foundation;

/// <summary>
/// Semantic application version. Generated at build-time from git tag or read from
/// VERSION file in CI; in dev returns a sentinel "0.0.0-dev+local".
/// </summary>
public readonly record struct AppVersion(int Major, int Minor, int Patch, string PreRelease, string Build)
{
    public static AppVersion Dev { get; } = new(0, 0, 0, "dev", "local");

    public bool IsDev => Major == 0 && Minor == 0 && Patch == 0 && PreRelease == "dev";

    public override string ToString()
    {
        var core = string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");
        if (!string.IsNullOrEmpty(PreRelease))
        {
            core += "-" + PreRelease;
        }

        if (!string.IsNullOrEmpty(Build))
        {
            core += "+" + Build;
        }

        return core;
    }

    public static AppVersion Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Dev;
        }

        var build = string.Empty;
        var plusIdx = text.IndexOf('+');
        if (plusIdx >= 0)
        {
            build = text[(plusIdx + 1) ..];
            text = text[..plusIdx];
        }

        var pre = string.Empty;
        var dashIdx = text.IndexOf('-');
        if (dashIdx >= 0)
        {
            pre = text[(dashIdx + 1) ..];
            text = text[..dashIdx];
        }

        var parts = text.Split('.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minor)
            || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var patch))
        {
            throw new FormatException($"Not a semver string: '{text}'.");
        }

        return new AppVersion(major, minor, patch, pre, build);
    }
}
