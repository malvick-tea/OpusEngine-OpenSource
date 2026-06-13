using System;
using System.IO;
using Opus.Foundation;

namespace Opus.App.Editor.Run;

/// <summary>
/// The shared document IO boundary behind the editor's per-document file stores: read a JSON document into
/// a typed result, and write one atomically (temp file then replace) so a crash mid-write never corrupts an
/// existing file. The pure serialisation is injected as delegates, so this single helper serves the scene,
/// animation-graph, and project stores without duplicating the filesystem logic. A filesystem fault returns
/// <see cref="ErrorCode.SaveIoFailed"/>; a malformed payload returns whatever the deserialiser reports
/// (typically <see cref="ErrorCode.SettingsCorrupt"/>). Never throws for an expected IO or format fault.
/// </summary>
internal static class EditorDocumentFile
{
    private const string TempSuffix = ".tmp";

    public static Result<T> Load<T>(string path, Func<string, Result<T>> deserialize, string label)
        where T : class
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(deserialize);
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Result<T>.Err(ErrorCode.SaveIoFailed, $"Cannot read {label} '{path}': {ex.Message}");
        }

        return deserialize(json);
    }

    public static Result Save<T>(string path, T document, Func<T, string> serialize, string label)
        where T : class
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(serialize);
        string json = serialize(document);
        try
        {
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath) ?? ".";
            Directory.CreateDirectory(directory);
            string temp = fullPath + TempSuffix;
            File.WriteAllText(temp, json);
            File.Move(temp, fullPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Result.Err(ErrorCode.SaveIoFailed, $"Cannot write {label} '{path}': {ex.Message}");
        }

        return Result.Ok();
    }
}
