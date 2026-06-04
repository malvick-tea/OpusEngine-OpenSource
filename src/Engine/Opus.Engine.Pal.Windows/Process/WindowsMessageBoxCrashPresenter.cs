using System;
using System.Text;
using Opus.Engine.Pal.Process;

namespace Opus.Engine.Pal.Windows.Process;

/// <summary>Win32 <see cref="ICrashReportPresenter"/>. Composes a player-readable
/// summary of the <see cref="CrashReport"/> + the on-disk dump path, then shows a
/// Yes/No message box. Yes → <see cref="CrashReportUserChoice.Restart"/>; No → Quit.
/// Any unexpected presenter failure falls through to Quit so the host still terminates.</summary>
public sealed class WindowsMessageBoxCrashPresenter : ICrashReportPresenter
{
    /// <summary>Dialog title shown above the report.</summary>
    public const string DialogCaption = "Opus crashed";

    public CrashReportUserChoice Show(CrashReport report)
    {
        try
        {
            var text = BuildDialogText(report);
            var result = WindowsMessageBox.Show(
                text,
                DialogCaption,
                WindowsMessageBox.Buttons.YesNo,
                WindowsMessageBox.Icon.Error);
            return result == WindowsMessageBox.Result.Yes
                ? CrashReportUserChoice.Restart
                : CrashReportUserChoice.Quit;
        }
        catch (Exception)
        {
            return CrashReportUserChoice.Quit;
        }
    }

    /// <summary>Builds the text body of the crash dialog. Public for tests so the
    /// composition format is locked without driving Win32 in a unit test.</summary>
    public static string BuildDialogText(CrashReport report)
    {
        var b = new StringBuilder();
        b.AppendLine("Opus crashed unexpectedly.");
        b.AppendLine();
        b.Append("Exception: ").Append(report.ExceptionType).Append(": ").AppendLine(report.Message);
        b.AppendLine();
        b.Append("Crash log: ").AppendLine(report.MinidumpPath);
        b.AppendLine();
        b.AppendLine("Restart Opus now?");
        return b.ToString();
    }
}
