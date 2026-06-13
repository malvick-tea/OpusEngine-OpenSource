using System.Globalization;
using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>The modal text-entry gestures of the <see cref="ViewportController"/>: renaming an element or the
/// scene, editing one numeric inspector field, and naming a save-as file. Each owns a small buffer state and
/// is mutually exclusive (one modal at a time); while any is active the input mapper routes typed keys here
/// and suppresses every other shortcut. Commits land as one undoable document edit (rename / field) or a
/// trimmed file name handed up to the app layer (save-as).</summary>
public sealed partial class ViewportController
{
    /// <summary>The longest field-edit buffer accepted — no finite float needs more characters.</summary>
    private const int MaxFieldEditLength = 16;

    private RenameState? _rename;
    private FieldEditState? _fieldEdit;
    private SaveAsState? _saveAs;

    /// <summary>The in-progress rename, or null. While non-null the window is modal: the input mapper
    /// feeds typed characters here and suppresses every other shortcut.</summary>
    public RenameState? Rename => _rename;

    /// <summary>The in-progress inspector numeric edit, or null. Modal exactly like a rename.</summary>
    public FieldEditState? FieldEdit => _fieldEdit;

    /// <summary>The in-progress save-as file-name entry, or null. Modal exactly like a rename; the commit
    /// is handed up to the app layer, which owns the file write.</summary>
    public SaveAsState? SaveAs => _saveAs;

    /// <summary>True while any modal text entry (rename, field edit, or save-as) is active — the window's
    /// click panels stand down and the input mapper routes keys to the active buffer.</summary>
    public bool IsTextEntryActive => _rename is not null || _fieldEdit is not null || _saveAs is not null;

    /// <summary>Starts renaming the selected element (Ctrl+R), seeding the buffer with its current name —
    /// or, with nothing selected, renaming the scene document itself (the buffer seeds with the document
    /// name and the commit lands as <see cref="EditorDocument.RenameDocument"/>). A second call while
    /// renaming restarts from the current name.</summary>
    public bool BeginRename()
    {
        var element = _document.SelectedElement;
        string? current = element switch
        {
            { IsNode: true } => _document.Scene.Find(element.AsNode)?.Name,
            { IsLight: true } => _document.Scene.FindLight(element.AsLight)?.Name,
            _ => _document.Name,
        };

        if (current is null)
        {
            return false;
        }

        _rename = new RenameState(element, current);
        return true;
    }

    /// <summary>Appends one typed character to the rename buffer, up to <see cref="MaxNameLength"/>.</summary>
    public void RenameAppend(char character)
    {
        if (_rename is { } rename && rename.Buffer.Length < MaxNameLength)
        {
            _rename = rename with { Buffer = rename.Buffer + character };
        }
    }

    /// <summary>Deletes the last character of the rename buffer (Backspace).</summary>
    public void RenameBackspace()
    {
        if (_rename is { Buffer.Length: > 0 } rename)
        {
            _rename = rename with { Buffer = rename.Buffer[..^1] };
        }
    }

    /// <summary>Abandons the rename, leaving the element's name untouched (Esc).</summary>
    public void CancelRename() => _rename = null;

    /// <summary>Commits the rename buffer as one undoable edit (Enter). A buffer that trims to nothing
    /// cancels instead — an element never takes an empty name. False when nothing was renamed.</summary>
    public bool CommitRename()
    {
        if (_rename is not { } rename)
        {
            return false;
        }

        _rename = null;
        string name = rename.Buffer.Trim();
        if (name.Length == 0)
        {
            return false;
        }

        if (rename.Element.IsNode)
        {
            return _document.RenameNode(rename.Element.AsNode, name);
        }

        if (rename.Element.IsLight)
        {
            return _document.Scene.FindLight(rename.Element.AsLight) is { } light &&
                _document.SetLight(light.WithName(name));
        }

        // A rename begun with nothing selected targets the scene document itself.
        return _document.RenameDocument(name);
    }

    /// <summary>Starts a save-as file-name entry (Ctrl+Shift+S), seeding the buffer with the document name
    /// so saving under the scene's own name is one Enter away. Any other text entry cancels — one modal at
    /// a time.</summary>
    public void BeginSaveAs()
    {
        _rename = null;
        _fieldEdit = null;
        _saveAs = new SaveAsState(_document.Name);
    }

    /// <summary>Appends one typed character to the save-as buffer, up to <see cref="MaxNameLength"/>.</summary>
    public void SaveAsAppend(char character)
    {
        if (_saveAs is { } saveAs && saveAs.Buffer.Length < MaxNameLength)
        {
            _saveAs = saveAs with { Buffer = saveAs.Buffer + character };
        }
    }

    /// <summary>Deletes the last character of the save-as buffer (Backspace).</summary>
    public void SaveAsBackspace()
    {
        if (_saveAs is { Buffer.Length: > 0 } saveAs)
        {
            _saveAs = saveAs with { Buffer = saveAs.Buffer[..^1] };
        }
    }

    /// <summary>Abandons the save-as without writing anything (Esc).</summary>
    public void CancelSaveAs() => _saveAs = null;

    /// <summary>Ends the save-as (Enter), returning the trimmed file name for the app layer to write, or
    /// null when no save-as was active or the buffer trims to nothing (then nothing is saved).</summary>
    public string? CommitSaveAs()
    {
        if (_saveAs is not { } saveAs)
        {
            return null;
        }

        _saveAs = null;
        string name = saveAs.Buffer.Trim();
        return name.Length == 0 ? null : name;
    }

    /// <summary>Starts a numeric edit of <paramref name="field"/> on the selected element (an inspector row
    /// click). The buffer starts empty — the author types the new value directly. The
    /// <see cref="InspectorField.Name"/> field routes to <see cref="BeginRename"/> instead, so clicking the
    /// name row behaves like Ctrl+R. False when nothing is selected or the field does not apply to the
    /// selected element. A field edit cancels any rename in progress and vice versa (one modal at a time).</summary>
    public bool BeginFieldEdit(InspectorField field)
    {
        if (field == InspectorField.Name)
        {
            _fieldEdit = null;
            return BeginRename();
        }

        var element = _document.SelectedElement;
        float? current = element switch
        {
            { IsNode: true } when _document.Scene.Find(element.AsNode) is { } node =>
                InspectorFieldAccess.Read(node.Transform, field),
            { IsLight: true } when _document.Scene.FindLight(element.AsLight) is { } light =>
                InspectorFieldAccess.Read(light, field),
            _ => null,
        };

        if (current is null)
        {
            return false;
        }

        _rename = null;
        _fieldEdit = new FieldEditState(element, field, string.Empty);
        return true;
    }

    /// <summary>Appends one typed character to the field-edit buffer. Only the numeric charset (digits,
    /// '.', '-') is accepted, and the buffer caps at a length no float needs to exceed.</summary>
    public void FieldEditAppend(char character)
    {
        if (_fieldEdit is { } edit && edit.Buffer.Length < MaxFieldEditLength &&
            (char.IsAsciiDigit(character) || character is '.' or '-'))
        {
            _fieldEdit = edit with { Buffer = edit.Buffer + character };
        }
    }

    /// <summary>Deletes the last character of the field-edit buffer (Backspace).</summary>
    public void FieldEditBackspace()
    {
        if (_fieldEdit is { Buffer.Length: > 0 } edit)
        {
            _fieldEdit = edit with { Buffer = edit.Buffer[..^1] };
        }
    }

    /// <summary>Abandons the field edit, leaving the element untouched (Esc).</summary>
    public void CancelFieldEdit() => _fieldEdit = null;

    /// <summary>Commits the field-edit buffer as one undoable edit (Enter): a node field lands as a
    /// transform replacement, a light field as a light replacement. A buffer that is empty or does not
    /// parse as a finite invariant float cancels instead, leaving the element unchanged. False when
    /// nothing was committed.</summary>
    public bool CommitFieldEdit()
    {
        if (_fieldEdit is not { } edit)
        {
            return false;
        }

        _fieldEdit = null;
        if (!float.TryParse(edit.Buffer, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ||
            !float.IsFinite(value))
        {
            return false;
        }

        if (edit.Element.IsNode && _document.Scene.Find(edit.Element.AsNode) is { } node)
        {
            return InspectorFieldAccess.Apply(node.Transform, edit.Field, value) is { } transform &&
                _document.TransformNode(node.Id, transform);
        }

        if (edit.Element.IsLight && _document.Scene.FindLight(edit.Element.AsLight) is { } light)
        {
            return InspectorFieldAccess.Apply(light, edit.Field, value) is { } changed &&
                _document.SetLight(changed);
        }

        return false;
    }
}
