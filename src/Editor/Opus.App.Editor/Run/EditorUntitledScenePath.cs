using System;
using System.Globalization;
using System.IO;

namespace Opus.App.Editor.Run;

/// <summary>
/// Picks the file an untitled window saves to: "untitled.scene.json" in the working directory, or the
/// first free "untitled-N.scene.json" when earlier untitled scenes already sit there — a fresh save never
/// silently overwrites one. Pure decision logic: the caller supplies the existence probe, so the rule is
/// unit-tested without touching a filesystem.
/// </summary>
public static class EditorUntitledScenePath
{
    public const string BaseName = "untitled";
    public const string Extension = ".scene.json";

    /// <summary>The first untitled scene path under <paramref name="directory"/> that
    /// <paramref name="exists"/> reports free.</summary>
    public static string Next(string directory, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(exists);
        string first = Path.Combine(directory, BaseName + Extension);
        if (!exists(first))
        {
            return first;
        }

        for (int suffix = 2; ; suffix++)
        {
            string candidate = Path.Combine(
                directory, string.Create(CultureInfo.InvariantCulture, $"{BaseName}-{suffix}{Extension}"));
            if (!exists(candidate))
            {
                return candidate;
            }
        }
    }
}
