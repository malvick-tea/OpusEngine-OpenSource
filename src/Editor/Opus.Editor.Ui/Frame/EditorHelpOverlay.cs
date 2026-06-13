using System;
using System.Collections.Generic;

namespace Opus.Editor.Ui;

/// <summary>One help-overlay row: the key (or gesture) and what it does.</summary>
/// <param name="Keys">The key chord or pointer gesture, as displayed.</param>
/// <param name="Description">The localised action description.</param>
public readonly record struct HelpEntry(string Keys, string Description);

/// <summary>The composed help overlay for one frame: its panel rect, localised title, and rows.</summary>
/// <param name="Panel">The overlay's pixel rect, centred in the viewport.</param>
/// <param name="Title">The localised overlay title.</param>
/// <param name="Entries">The shortcut rows, top to bottom.</param>
public sealed record EditorHelpView(EditorPanelRect Panel, string Title, IReadOnlyList<HelpEntry> Entries);

/// <summary>
/// The window's F1 shortcut reference: the full localised table of every mouse gesture and key the editor
/// understands, laid out as a panel centred over the viewport. Lives in one place so a new shortcut only
/// has to be added here (and to the status-line hint) to be discoverable. Pure — the composer builds the
/// view and the drawer replays it.
/// </summary>
public static class EditorHelpOverlay
{
    public const int RowHeight = 20;
    public const int HeaderHeight = 30;
    public const int PaddingX = 14;
    public const int PaddingY = 10;
    public const int PreferredWidth = 560;
    public const int KeysColumnWidth = 150;
    public const int ViewportMargin = 10;

    private static readonly IReadOnlyList<HelpEntry> EnglishEntries = new HelpEntry[]
    {
        new("LMB drag", "orbit the camera"),
        new("MMB drag", "pan the camera"),
        new("Wheel", "zoom (scrolls the outliner over it)"),
        new("Click", "select the node or light under the cursor"),
        new("Ctrl+Click", "add or remove an element from the multi-selection"),
        new("Shift+Drag", "box select what the rectangle contains (with Ctrl: add to the selection)"),
        new("Shift+Click row", "select the outliner range from the primary to the clicked row"),
        new("Ctrl+A", "select every visible element"),
        new("Click value", "edit the property in the inspector (Enter applies, Esc cancels)"),
        new("1 - 5", "add a shape: cube / sphere / cylinder / plane / cone"),
        new("A", "add an empty node at the camera target"),
        new("L", "add a point light at the camera target"),
        new("M", "place a model from the content root"),
        new("W / E / R", "gizmo mode: move / scale / rotate"),
        new("Drag axis", "transform the selection (hold Ctrl to snap)"),
        new("Drag selection", "slide it on its ground plane (hold Ctrl to snap)"),
        new("Arrows", "nudge the selection one metre along the grid (X / Z)"),
        new("F", "frame the selection (or the whole scene without one)"),
        new("H", "reset the camera to the home view"),
        new("V", "hide or show the selection (unhide via the outliner)"),
        new("P / Shift+P", "parent the selection under the primary / detach to a root"),
        new("Del", "delete the selection"),
        new("Ctrl+R", "rename the selection, or the scene with nothing selected"),
        new("Ctrl+D", "duplicate the selection"),
        new("Ctrl+G", "group the selected nodes under a new parent"),
        new("Ctrl+Shift+G", "ungroup the selected groups (children rise to the parent)"),
        new("Ctrl+C / Ctrl+V", "copy / paste the selection (pastes at the camera target)"),
        new("Ctrl+Z / Ctrl+Y", "undo / redo"),
        new("Ctrl+S", "save the scene"),
        new("Ctrl+Shift+S", "save the scene to a new file (type the name, Enter saves)"),
        new("Ctrl+N", "new scene (the current one is saved first)"),
        new("Ctrl+O", "open a scene from the working folder (saves the current one)"),
        new("F1", "toggle this help"),
        new("F2", "save a screenshot"),
        new("F3", "toggle the developer stats overlay"),
        new("Esc", "quit the editor"),
    };

    private static readonly IReadOnlyList<HelpEntry> RussianEntries = new HelpEntry[]
    {
        new("ЛКМ тянуть", "орбита камеры"),
        new("СКМ тянуть", "панорама камеры"),
        new("Колесо", "зум (над деревом сцены — прокрутка)"),
        new("Клик", "выбрать узел или свет под курсором"),
        new("Ctrl+клик", "добавить или убрать элемент из мультивыбора"),
        new("Shift+тянуть", "рамочный выбор содержимого рамки (с Ctrl — добавить к выбору)"),
        new("Shift+клик по строке", "выбрать диапазон дерева от основного до этой строки"),
        new("Ctrl+A", "выбрать все видимые элементы"),
        new("Клик по значению", "править свойство в инспекторе (Enter — принять, Esc — отмена)"),
        new("1 - 5", "добавить фигуру: куб / сфера / цилиндр / плоскость / конус"),
        new("A", "добавить пустой узел в цель камеры"),
        new("L", "добавить точечный свет в цель камеры"),
        new("M", "поставить модель из корня контента"),
        new("W / E / R", "режим гизмо: перемещение / масштаб / поворот"),
        new("Тянуть ось", "трансформировать выбор (Ctrl — привязка)"),
        new("Тянуть выбранное", "скольжение по плоскости земли (Ctrl — привязка)"),
        new("Стрелки", "сдвинуть выбор на метр по сетке (X / Z)"),
        new("F", "кадрировать выбор (без выбора — всю сцену)"),
        new("H", "вернуть камеру в исходный вид"),
        new("V", "скрыть или показать выбор (вернуть — через дерево сцены)"),
        new("P / Shift+P", "сделать выбор дочерним к основному / открепить в корень"),
        new("Del", "удалить выбор"),
        new("Ctrl+R", "переименовать выбор, без выбора — сцену"),
        new("Ctrl+D", "дублировать выбор"),
        new("Ctrl+G", "сгруппировать узлы под новым родителем"),
        new("Ctrl+Shift+G", "разгруппировать выбранные группы (дети поднимаются к родителю)"),
        new("Ctrl+C / Ctrl+V", "копировать / вставить выбор (вставка в цель камеры)"),
        new("Ctrl+Z / Ctrl+Y", "отмена / повтор"),
        new("Ctrl+S", "сохранить сцену"),
        new("Ctrl+Shift+S", "сохранить сцену в новый файл (введите имя, Enter — сохранить)"),
        new("Ctrl+N", "новая сцена (текущая сохраняется)"),
        new("Ctrl+O", "открыть сцену из рабочей папки (текущая сохраняется)"),
        new("F1", "показать или скрыть эту справку"),
        new("F2", "сохранить снимок окна"),
        new("F3", "показать или скрыть статистику разработчика"),
        new("Esc", "выйти из редактора"),
    };

    /// <summary>The localised shortcut rows.</summary>
    public static IReadOnlyList<HelpEntry> Entries(EditorLanguage language) =>
        language == EditorLanguage.Russian ? RussianEntries : EnglishEntries;

    /// <summary>The localised overlay title.</summary>
    public static string Title(EditorLanguage language) =>
        language == EditorLanguage.Russian ? "Горячие клавиши" : "Shortcuts";

    /// <summary>Lays the overlay out centred in <paramref name="viewport"/>, clamped to fit inside it.</summary>
    public static EditorHelpView Build(EditorPanelRect viewport, EditorLanguage language)
    {
        var entries = Entries(language);
        int width = Math.Min(PreferredWidth, Math.Max(0, viewport.Width - (2 * ViewportMargin)));
        int preferredHeight = HeaderHeight + (entries.Count * RowHeight) + PaddingY;
        int height = Math.Min(preferredHeight, Math.Max(0, viewport.Height - (2 * ViewportMargin)));
        int x = viewport.X + ((viewport.Width - width) / 2);
        int y = viewport.Y + ((viewport.Height - height) / 2);
        return new EditorHelpView(new EditorPanelRect(x, y, width, height), Title(language), entries);
    }
}
