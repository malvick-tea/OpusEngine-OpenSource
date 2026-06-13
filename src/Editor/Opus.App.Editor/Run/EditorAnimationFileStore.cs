using Opus.Editor.Core;
using Opus.Foundation;

namespace Opus.App.Editor.Run;

/// <summary>
/// The editor's animation-graph IO boundary: load a graph document from disk into a typed result, and save
/// one atomically. The pure serialisation lives in <see cref="AnimationGraphSerializer"/> (Editor.Core) and
/// the shared filesystem logic in <see cref="EditorDocumentFile"/>; this type only binds the two for graphs.
/// </summary>
public static class EditorAnimationFileStore
{
    private const string Label = "animation graph";

    /// <summary>Reads and parses a graph file. A filesystem error returns
    /// <see cref="ErrorCode.SaveIoFailed"/>; a malformed or wrong-version file returns the serialiser's
    /// <see cref="ErrorCode.SettingsCorrupt"/>. Never throws for an expected IO or format fault.</summary>
    public static Result<AnimationGraphDocument> Load(string path) =>
        EditorDocumentFile.Load(path, AnimationGraphSerializer.Deserialize, Label);

    /// <summary>Serialises and writes a graph atomically (temp file then replace). Returns
    /// <see cref="ErrorCode.SaveIoFailed"/> on a filesystem fault instead of throwing.</summary>
    public static Result Save(string path, AnimationGraphDocument document) =>
        EditorDocumentFile.Save(path, document, AnimationGraphSerializer.Serialize, Label);
}
