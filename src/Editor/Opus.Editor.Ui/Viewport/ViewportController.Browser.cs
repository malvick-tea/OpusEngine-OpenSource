using System;
using System.Collections.Generic;
using System.IO;
using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>The modal file-browser overlay of the <see cref="ViewportController"/>: the Ctrl+O open-scene
/// list and the M place-model list share one <see cref="SceneBrowserState"/>, told apart by purpose. The app
/// layer supplies the directory listing; confirming an open-scene reports the choice up (the file load is IO),
/// while confirming a place-model is a pure document edit completed here.</summary>
public sealed partial class ViewportController
{
    /// <summary>The open-scene / place-model browser overlay state, or null while closed. Modal like a text
    /// entry.</summary>
    public SceneBrowserState? SceneBrowser { get; private set; }

    /// <summary>Opens the scene browser over <paramref name="files"/> (the app layer's directory listing),
    /// highlighting the first entry. Any text entry in progress is cancelled — one modal at a time.</summary>
    public void OpenSceneBrowser(IReadOnlyList<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        _rename = null;
        _fieldEdit = null;
        _saveAs = null;
        SceneBrowser = new SceneBrowserState(files, 0);
    }

    /// <summary>Opens the place-model browser over <paramref name="assetRefs"/> (the app layer's
    /// content-root listing of model files, as content-root-relative refs), highlighting the first entry.
    /// Confirming places the chosen model at the camera target (<see cref="PlaceModelFromBrowser"/>).
    /// Any text entry in progress is cancelled — one modal at a time.</summary>
    public void OpenModelBrowser(IReadOnlyList<string> assetRefs)
    {
        ArgumentNullException.ThrowIfNull(assetRefs);
        _rename = null;
        _fieldEdit = null;
        _saveAs = null;
        SceneBrowser = new SceneBrowserState(assetRefs, 0, BrowserPurpose.PlaceModel);
    }

    /// <summary>Places the model browser's highlighted asset at the camera target as one undoable edit —
    /// a node named from the file stem carrying the asset ref, selected — and closes the browser. The
    /// model-bounds source resolves the ref, so the new node draws and picks with its real bounds.
    /// No-op (returns <see cref="SceneNodeId.None"/>) when the browser is closed, not in place-model
    /// purpose, or empty.</summary>
    public SceneNodeId PlaceModelFromBrowser()
    {
        if (SceneBrowser is not { Purpose: BrowserPurpose.PlaceModel } || SceneBrowserChoice is not { } assetRef)
        {
            return SceneNodeId.None;
        }

        SceneBrowser = null;
        string name = Path.GetFileNameWithoutExtension(assetRef);
        if (name.Length == 0)
        {
            name = "model";
        }

        var transform = EditorTransform.Identity with { Position = Float3.FromVector3(Camera.Target) };
        return _document.PlaceNode(name, assetRef, transform);
    }

    /// <summary>Closes the scene browser without opening anything (Esc).</summary>
    public void CloseSceneBrowser() => SceneBrowser = null;

    /// <summary>Moves the browser highlight by <paramref name="delta"/> rows, clamped to the list.</summary>
    public void MoveSceneBrowserHighlight(int delta)
    {
        if (SceneBrowser is { } browser && browser.Files.Count > 0)
        {
            int clamped = Math.Clamp(EditorSceneBrowser.ClampHighlight(browser) + delta, 0, browser.Files.Count - 1);
            SceneBrowser = browser with { Highlight = clamped };
        }
    }

    /// <summary>Sets the browser highlight to an absolute row (a pointer hover / click).</summary>
    public void SetSceneBrowserHighlight(int index)
    {
        if (SceneBrowser is { } browser && index >= 0 && index < browser.Files.Count)
        {
            SceneBrowser = browser with { Highlight = index };
        }
    }

    /// <summary>The highlighted scene file path, or null when the browser is closed or empty — what an
    /// Enter / row click asks the app layer to open.</summary>
    public string? SceneBrowserChoice =>
        SceneBrowser is { } browser && EditorSceneBrowser.ClampHighlight(browser) is var index && index >= 0
            ? browser.Files[index]
            : null;
}
