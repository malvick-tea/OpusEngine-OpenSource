namespace Opus.App.Editor.Cli;

/// <summary>The editor CLI option names in one place, so the per-command parsers and the argument reader
/// share the exact spelling and a renamed option changes only here.</summary>
internal static class CliOptions
{
    public const string NameOption = "--name";
    public const string AtOption = "--at";
    public const string EulerOption = "--euler";
    public const string ScaleOption = "--scale";
    public const string ContentRootOption = "--content-root";
    public const string ClipOption = "--clip";
    public const string LoopOption = "--loop";
    public const string SpeedOption = "--speed";
    public const string EntryFlag = "--entry";
    public const string OnOption = "--on";
    public const string BlendOption = "--blend";
    public const string FramesOption = "--frames";
    public const string SettingsOption = "--settings";
    public const string LanguageOption = "--lang";
    public const string ProjectOption = "--project";
    public const string ColorOption = "--color";
    public const string IntensityOption = "--intensity";
    public const string DirectionOption = "--dir";
    public const string RangeOption = "--range";
    public const string ConeOption = "--cone";
}
