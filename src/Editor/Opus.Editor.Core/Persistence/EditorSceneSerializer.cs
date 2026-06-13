using System;
using Opus.Foundation;
using Opus.Persistence.Settings;

namespace Opus.Editor.Core;

/// <summary>
/// Reads and writes an <see cref="EditorSceneDocument"/> as human-editable JSON inside the engine's
/// versioned settings envelope, reusing <see cref="JsonSettingsSerializer"/> so the scene file shares the
/// same indented camel-case shape and the same corruption-safe load contract (a malformed or
/// wrong-version file returns a typed <see cref="ErrorCode.SettingsCorrupt"/> error instead of throwing).
/// Pure: no file IO — that is the host's job, matching the persistence module contract.
/// </summary>
public static class EditorSceneSerializer
{
    /// <summary>On-disk schema version of the scene payload. Bump when <see cref="EditorSceneDocument"/>
    /// or its node shape changes so an older reader rejects a newer file.</summary>
    public const int SchemaVersion = 1;

    public static string Serialize(EditorSceneDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSettingsSerializer.Serialize(document, SchemaVersion);
    }

    public static Result<EditorSceneDocument> Deserialize(string json) =>
        JsonSettingsSerializer.Deserialize<EditorSceneDocument>(json, SchemaVersion);
}
