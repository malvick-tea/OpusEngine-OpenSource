using Opus.Editor.Core;
using Opus.Foundation;

namespace Opus.App.Editor.Run;

/// <summary>
/// The editor's scene-file IO boundary: load a scene document from disk into a typed result, and save one
/// atomically. The pure serialisation lives in <see cref="EditorSceneSerializer"/> (Editor.Core) and the
/// shared filesystem logic in <see cref="EditorDocumentFile"/>; this type only binds the two for scenes.
/// </summary>
public static class EditorSceneFileStore
{
    private const string Label = "scene";

    /// <summary>Reads and parses a scene file. A filesystem error returns
    /// <see cref="ErrorCode.SaveIoFailed"/>; a malformed or wrong-version file returns the serialiser's
    /// <see cref="ErrorCode.SettingsCorrupt"/>. Never throws for an expected IO or format fault.</summary>
    public static Result<EditorSceneDocument> Load(string path) =>
        EditorDocumentFile.Load(path, EditorSceneSerializer.Deserialize, Label);

    /// <summary>Serialises and writes a scene atomically (temp file then replace). Returns
    /// <see cref="ErrorCode.SaveIoFailed"/> on a filesystem fault instead of throwing.</summary>
    public static Result Save(string path, EditorSceneDocument document) =>
        EditorDocumentFile.Save(path, document, EditorSceneSerializer.Serialize, Label);
}
