using System;
using Opus.Foundation;
using Opus.Persistence.Settings;

namespace Opus.Editor.Core;

/// <summary>
/// Reads and writes an <see cref="EditorProjectDocument"/> as human-editable JSON inside the engine's
/// versioned settings envelope, reusing <see cref="JsonSettingsSerializer"/> so the project file shares
/// the same indented camel-case shape and the same corruption-safe load contract (a malformed or
/// wrong-version file returns a typed <see cref="ErrorCode.SettingsCorrupt"/> error instead of throwing).
/// Pure: no file IO — that is the host's job, matching the persistence module contract.
/// </summary>
public static class EditorProjectSerializer
{
    /// <summary>On-disk schema version of the project payload. Bump when
    /// <see cref="EditorProjectDocument"/> changes so an older reader rejects a newer file.</summary>
    public const int SchemaVersion = 1;

    public static string Serialize(EditorProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSettingsSerializer.Serialize(document, SchemaVersion);
    }

    public static Result<EditorProjectDocument> Deserialize(string json) =>
        JsonSettingsSerializer.Deserialize<EditorProjectDocument>(json, SchemaVersion);
}
