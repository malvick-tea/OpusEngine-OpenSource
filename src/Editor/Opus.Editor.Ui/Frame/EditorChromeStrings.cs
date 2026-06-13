namespace Opus.Editor.Ui;

/// <summary>The display language for the editor window chrome.</summary>
public enum EditorLanguage
{
    /// <summary>English chrome (the default).</summary>
    English,

    /// <summary>Russian chrome.</summary>
    Russian,
}

/// <summary>
/// The editor window's chrome labels for one language: the toolbar title parts, the pseudo-code panel
/// header, the status-line labels, and the controls hint. Centralised (rather than literals scattered
/// through the composer and drawer) so the window localises in one place and the strings are a named
/// concept, not magic strings. The editor's glyph atlas bakes Latin + the full Cyrillic block, so the
/// Russian chrome renders deterministically (ADR-0034).
/// </summary>
/// <param name="ApplicationName">The product name shown in the toolbar (not translated).</param>
/// <param name="DirtyMarker">The unsaved-document marker appended to the toolbar title.</param>
/// <param name="PseudoCodeHeader">The header above the live pseudo-code mirror panel.</param>
/// <param name="NoSelection">The status-line text when nothing is selected.</param>
/// <param name="SelectedPrefix">The status-line prefix before a selected node's id and name.</param>
/// <param name="SelectedLightPrefix">The status-line prefix before a selected light's id and name.</param>
/// <param name="SelectedCountLabel">The status-line label before the member count of a multi-selection.</param>
/// <param name="NodesLabel">The status-line label before the node count.</param>
/// <param name="LightsLabel">The status-line label before the light count.</param>
/// <param name="ControlsHint">The status-line controls reminder.</param>
/// <param name="AddNodeButton">The toolbar add-node-button label.</param>
/// <param name="AddLightButton">The toolbar add-light-button label.</param>
/// <param name="AddCubeButton">The toolbar add-cube-primitive-button label.</param>
/// <param name="AddSphereButton">The toolbar add-sphere-primitive-button label.</param>
/// <param name="AddCylinderButton">The toolbar add-cylinder-primitive-button label.</param>
/// <param name="AddPlaneButton">The toolbar add-plane-primitive-button label.</param>
/// <param name="AddConeButton">The toolbar add-cone-primitive-button label.</param>
/// <param name="AddModelButton">The toolbar place-model-button label (opens the model browser).</param>
/// <param name="SaveButton">The toolbar save-document-button label.</param>
/// <param name="UndoButton">The toolbar undo-button label.</param>
/// <param name="RedoButton">The toolbar redo-button label.</param>
/// <param name="DeleteButton">The toolbar delete-selection-button label.</param>
/// <param name="FrameButton">The toolbar frame-selection-button label.</param>
/// <param name="OutlinerHeader">The header above the scene outliner panel.</param>
/// <param name="HiddenSuffix">The marker appended to an outliner row whose element is hidden.</param>
/// <param name="InspectorHeader">The header above the selection properties panel.</param>
/// <param name="SceneBrowserTitle">The title of the Ctrl+O open-scene overlay.</param>
/// <param name="SceneBrowserEmpty">The hint shown in the open-scene overlay when no scene files exist.</param>
/// <param name="ModelBrowserTitle">The title of the M place-model overlay.</param>
/// <param name="ModelBrowserEmpty">The hint shown in the place-model overlay when the content root has no models.</param>
/// <param name="RenamingLabel">The status-line label shown before the rename buffer while renaming.</param>
/// <param name="EditingLabel">The status-line label shown before the field-edit buffer while editing.</param>
/// <param name="GizmoLabel">The status-line label before the active gizmo mode name.</param>
/// <param name="MoveMode">The status-line name for the translate (move) gizmo mode.</param>
/// <param name="ScaleMode">The status-line name for the scale gizmo mode.</param>
/// <param name="RotateMode">The status-line name for the rotate gizmo mode.</param>
/// <param name="SaveAsLabel">The status-line label shown before the save-as buffer while naming the file.</param>
public sealed record EditorChromeStrings(
    string ApplicationName,
    string DirtyMarker,
    string PseudoCodeHeader,
    string NoSelection,
    string SelectedPrefix,
    string SelectedLightPrefix,
    string SelectedCountLabel,
    string NodesLabel,
    string LightsLabel,
    string ControlsHint,
    string AddNodeButton,
    string AddLightButton,
    string AddCubeButton,
    string AddSphereButton,
    string AddCylinderButton,
    string AddPlaneButton,
    string AddConeButton,
    string AddModelButton,
    string SaveButton,
    string UndoButton,
    string RedoButton,
    string DeleteButton,
    string FrameButton,
    string OutlinerHeader,
    string HiddenSuffix,
    string InspectorHeader,
    string SceneBrowserTitle,
    string SceneBrowserEmpty,
    string ModelBrowserTitle,
    string ModelBrowserEmpty,
    string RenamingLabel,
    string EditingLabel,
    string GizmoLabel,
    string MoveMode,
    string ScaleMode,
    string RotateMode,
    string SaveAsLabel)
{
    /// <summary>Which language these strings carry — the help overlay derives its localised table from
    /// this, so the chrome and the overlay can never disagree.</summary>
    public EditorLanguage Language { get; init; }

    public static readonly EditorChromeStrings English = new(
        ApplicationName: "Opus Editor",
        DirtyMarker: "*",
        PseudoCodeHeader: "pseudo-code",
        NoSelection: "no selection",
        SelectedPrefix: "selected #",
        SelectedLightPrefix: "selected light *",
        SelectedCountLabel: "selected:",
        NodesLabel: "nodes",
        LightsLabel: "lights",
        ControlsHint: "LMB orbit  MMB pan  wheel zoom  click select  Ctrl+click multi  Shift+drag box  Ctrl+A all  1-5 add shape  A/L node/light  M model  W/E/R move/scale/rotate  drag axis (Ctrl snap)  arrows nudge  F frame  H home  V hide  P/Shift+P parent/unparent  Del delete  Ctrl+R rename  Ctrl+D dup  Ctrl+G group  Ctrl+Shift+G ungroup  Ctrl+C/V copy/paste  Ctrl+Z/Y undo/redo  Ctrl+S save  Ctrl+Shift+S save as  Ctrl+N new  Ctrl+O open  F1 help  F2 shot  F3 stats  Esc quit",
        AddNodeButton: "+ Node",
        AddLightButton: "+ Light",
        AddCubeButton: "+ Cube",
        AddSphereButton: "+ Sphere",
        AddCylinderButton: "+ Cyl",
        AddPlaneButton: "+ Plane",
        AddConeButton: "+ Cone",
        AddModelButton: "+ Model",
        SaveButton: "Save",
        UndoButton: "Undo",
        RedoButton: "Redo",
        DeleteButton: "Delete",
        FrameButton: "Frame",
        OutlinerHeader: "scene tree",
        HiddenSuffix: "(hidden)",
        InspectorHeader: "properties",
        SceneBrowserTitle: "Open scene (Enter opens, Esc closes)",
        SceneBrowserEmpty: "no scene files in the working folder",
        ModelBrowserTitle: "Place model (Enter places, Esc closes)",
        ModelBrowserEmpty: "no model files under the content root",
        RenamingLabel: "renaming:",
        EditingLabel: "editing:",
        GizmoLabel: "gizmo",
        MoveMode: "move",
        ScaleMode: "scale",
        RotateMode: "rotate",
        SaveAsLabel: "save as:")
    {
        Language = EditorLanguage.English,
    };

    public static readonly EditorChromeStrings Russian = new(
        ApplicationName: "Opus Editor",
        DirtyMarker: "*",
        PseudoCodeHeader: "псевдокод",
        NoSelection: "нет выделения",
        SelectedPrefix: "выбран #",
        SelectedLightPrefix: "выбран свет *",
        SelectedCountLabel: "выбрано:",
        NodesLabel: "узлы",
        LightsLabel: "свет",
        ControlsHint: "ЛКМ орбита  СКМ панорама  колесо зум  клик выбор  Ctrl+клик мультивыбор  Shift+тянуть рамка  Ctrl+A всё  1-5 фигура  A/L узел/свет  M модель  W/E/R перемещ/масштаб/поворот  тянуть ось (Ctrl привязка)  стрелки сдвиг  F кадр  H камера  V скрыть  P/Shift+P родитель/открепить  Del удалить  Ctrl+R имя  Ctrl+D дубль  Ctrl+G группа  Ctrl+Shift+G разгруппировать  Ctrl+C/V копир/вставка  Ctrl+Z/Y отмена/повтор  Ctrl+S сохранить  Ctrl+Shift+S сохранить как  Ctrl+N новая  Ctrl+O открыть  F1 помощь  F2 снимок  F3 статистика  Esc выход",
        AddNodeButton: "+ Узел",
        AddLightButton: "+ Свет",
        AddCubeButton: "+ Куб",
        AddSphereButton: "+ Сфера",
        AddCylinderButton: "+ Цил",
        AddPlaneButton: "+ Плоск",
        AddConeButton: "+ Конус",
        AddModelButton: "+ Модель",
        SaveButton: "Сохранить",
        UndoButton: "Отмена",
        RedoButton: "Повтор",
        DeleteButton: "Удалить",
        FrameButton: "Кадр",
        OutlinerHeader: "дерево сцены",
        HiddenSuffix: "(скрыт)",
        InspectorHeader: "свойства",
        SceneBrowserTitle: "Открыть сцену (Enter — открыть, Esc — закрыть)",
        SceneBrowserEmpty: "в рабочей папке нет файлов сцен",
        ModelBrowserTitle: "Поставить модель (Enter — поставить, Esc — закрыть)",
        ModelBrowserEmpty: "под корнем контента нет файлов моделей",
        RenamingLabel: "переименование:",
        EditingLabel: "правка:",
        GizmoLabel: "гизмо",
        MoveMode: "перемещение",
        ScaleMode: "масштаб",
        RotateMode: "поворот",
        SaveAsLabel: "сохранить как:")
    {
        Language = EditorLanguage.Russian,
    };

    /// <summary>The chrome strings for a language.</summary>
    public static EditorChromeStrings For(EditorLanguage language) => language switch
    {
        EditorLanguage.Russian => Russian,
        _ => English,
    };

    /// <summary>The localised name of a gizmo mode, for the status-line mode indicator.</summary>
    public string GizmoModeName(GizmoMode mode) => mode switch
    {
        GizmoMode.Scale => ScaleMode,
        GizmoMode.Rotate => RotateMode,
        _ => MoveMode,
    };
}
