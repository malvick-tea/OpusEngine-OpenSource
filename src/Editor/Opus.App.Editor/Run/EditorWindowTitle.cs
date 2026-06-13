using System;
using Opus.Editor.Core;

namespace Opus.App.Editor.Run;

/// <summary>
/// Builds the OS window title for the live editor: the product name, the open document's name, and a
/// dirty marker while there are unsaved edits — so the taskbar and Alt-Tab always say which scene the
/// window holds, exactly like the in-window toolbar (which may truncate on narrow windows; the OS title
/// never does). Pure; the window runner syncs it to the window once per frame when it changes.
/// </summary>
public static class EditorWindowTitle
{
    /// <summary>The title for the current document state.</summary>
    public static string For(EditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        string title = $"{EditorWindowRunner.WindowTitle} — {document.Name}";
        return document.IsDirty ? title + " *" : title;
    }
}
