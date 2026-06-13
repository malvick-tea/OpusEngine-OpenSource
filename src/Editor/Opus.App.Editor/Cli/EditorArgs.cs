using Opus.Editor.Core;
using Opus.Editor.Ui;

namespace Opus.App.Editor.Cli;

/// <summary>
/// Parsed editor CLI arguments. Immutable; produced by <see cref="EditorCliParser"/>.
/// </summary>
/// <param name="Mode">Dispatch mode.</param>
/// <param name="ScenePath">Scene document path for New / Show / Dsl / Place, or a model path for Inspect.</param>
/// <param name="SceneName">Optional scene display name (New) or node name (Place); defaults to a file stem.</param>
/// <param name="HelpReason">Non-empty when Help signals a usage error; empty for plain help.</param>
/// <param name="AssetRef">Asset reference to place (Place mode), or the target node id for the scene-edit
/// commands (Remove / Rename / Move / Rotate / Scale / Duplicate), or null.</param>
/// <param name="Position">The Float3 argument for the command: a placement / move position, a rotation in
/// Euler degrees (Rotate), or a scale (Scale). Null means the origin / unset.</param>
/// <param name="ContentRoot">Root directory for resolving asset references (Report / Window modes), or null.</param>
/// <param name="Animation">Animation-command parameters (AnimState / AnimTransition modes), or null.</param>
/// <param name="WindowMaxFrames">Window-mode frame cap (0 = run until closed); a diagnostic affordance.</param>
/// <param name="SettingsPath">Window-mode settings file for layout persistence (opt-in), or null.</param>
/// <param name="Language">Window-mode chrome language (EN / RU) when given explicitly, or null to fall
/// back to the persisted / default language.</param>
/// <param name="Light">Light-command parameters (LightAdd mode), or null. The LightRemove target id reuses
/// <see cref="AssetRef"/>, like the scene-edit node commands.</param>
/// <param name="ProjectPath">Window-mode project manifest to open the window on (opt-in), or null. The
/// project's content roots feed the place-model browser and its scenes the open browser.</param>
public sealed record EditorArgs(
    EditorMode Mode,
    string? ScenePath,
    string? SceneName,
    string HelpReason,
    string? AssetRef = null,
    Float3? Position = null,
    string? ContentRoot = null,
    AnimationArgs? Animation = null,
    int WindowMaxFrames = 0,
    string? SettingsPath = null,
    EditorLanguage? Language = null,
    LightArgs? Light = null,
    string? ProjectPath = null)
{
    public static EditorArgs ForHelp(string reason) => new(EditorMode.Help, null, null, reason);
}
