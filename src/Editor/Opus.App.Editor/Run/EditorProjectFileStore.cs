using Opus.Editor.Core;
using Opus.Foundation;

namespace Opus.App.Editor.Run;

/// <summary>
/// The editor's project-manifest IO boundary: load a project document from disk into a typed result, and
/// save one atomically. The pure serialisation lives in <see cref="EditorProjectSerializer"/> (Editor.Core)
/// and the shared filesystem logic in <see cref="EditorDocumentFile"/>; this type only binds the two for
/// projects.
/// </summary>
public static class EditorProjectFileStore
{
    private const string Label = "project";

    /// <summary>Reads and parses a project file. A filesystem error returns
    /// <see cref="ErrorCode.SaveIoFailed"/>; a malformed or wrong-version file returns the serialiser's
    /// <see cref="ErrorCode.SettingsCorrupt"/>. Never throws for an expected IO or format fault.</summary>
    public static Result<EditorProjectDocument> Load(string path) =>
        EditorDocumentFile.Load(path, EditorProjectSerializer.Deserialize, Label);

    /// <summary>Serialises and writes a project atomically (temp file then replace). Returns
    /// <see cref="ErrorCode.SaveIoFailed"/> on a filesystem fault instead of throwing.</summary>
    public static Result Save(string path, EditorProjectDocument document) =>
        EditorDocumentFile.Save(path, document, EditorProjectSerializer.Serialize, Label);
}
