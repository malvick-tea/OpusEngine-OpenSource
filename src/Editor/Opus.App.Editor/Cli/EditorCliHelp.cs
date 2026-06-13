using System.IO;
using System.Text;
using Opus.Foundation;

namespace Opus.App.Editor.Cli;

/// <summary>
/// Produces the editor CLI usage banner the dispatcher prints on <c>help</c>, <c>--help</c>, or a parse
/// error. The per-command usage lines come from <see cref="EditorCliParser.Commands"/> — the same table that
/// drives dispatch — so a new command's help can never drift from its parser. The text is data only, so it
/// is testable without a console.
/// </summary>
public static class EditorCliHelp
{
    private const string DefaultLine =
        "  opus-editor                            Open the live D3D12 authoring window (default).";

    private const string HelpLine =
        "  opus-editor --help                       Print this help banner.";

    public static string Render(string reason)
    {
        var text = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(reason))
        {
            text.Append("error: ").AppendLine(reason);
            text.AppendLine();
        }

        text.Append(EngineIdentity.Current.DisplayName).AppendLine(" — UI Edition editor.");
        text.AppendLine();
        text.AppendLine("Usage:");
        text.AppendLine(DefaultLine);
        foreach (var command in EditorCliParser.Commands)
        {
            foreach (string line in command.UsageLines)
            {
                text.AppendLine(line);
            }
        }

        text.AppendLine(HelpLine);
        return text.ToString();
    }

    /// <summary>Writes the banner to the supplied writer; convenience for the dispatcher.</summary>
    public static void Write(TextWriter writer, string reason) => writer.Write(Render(reason));
}
