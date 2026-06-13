using System;
using System.Globalization;
using System.IO;

namespace Opus.App.Editor.Run;

/// <summary>
/// Builds the file paths the editor window writes screenshots to: a fixed subfolder of the working
/// directory, with sortable timestamped file names. The path building is pure (and tested); the directory
/// lookup uses the process working directory.
/// </summary>
public static class EditorScreenshotPath
{
    public const string DirectoryName = "opus-editor-screenshots";

    /// <summary>The directory editor screenshots are written to: a fixed subfolder of the working dir.</summary>
    public static string Directory() => Path.Combine(Environment.CurrentDirectory, DirectoryName);

    /// <summary>A sortable, collision-resistant screenshot file name for a capture time.</summary>
    public static string FileName(DateTimeOffset timestamp) =>
        string.Create(CultureInfo.InvariantCulture, $"opus-editor-{timestamp:yyyyMMdd-HHmmss-fff}.png");

    public static string Build(string directory, DateTimeOffset timestamp) =>
        Path.Combine(directory, FileName(timestamp));
}
