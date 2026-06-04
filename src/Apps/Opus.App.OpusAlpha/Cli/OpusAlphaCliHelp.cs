using System.IO;
using System.Text;

namespace Opus.App.OpusAlpha.Cli;

/// <summary>Produces the usage banner the dispatcher prints when the user passes
/// <c>--help</c>, <c>-h</c>, or hits a parser error. The text is data only; the
/// dispatcher writes it to stdout when <see cref="OpusAlphaMode.Help"/> is selected.</summary>
public static class OpusAlphaCliHelp
{
    /// <summary>Returns a multiline usage banner. Pass <paramref name="reason"/> when
    /// emitting the banner after a parser error so the operator sees what went wrong.</summary>
    public static string Render(string reason)
    {
        var text = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(reason))
        {
            text.Append("error: ").AppendLine(reason);
            text.AppendLine();
        }

        text.AppendLine("Opus 0.1 alpha host (M9 harness).");
        text.AppendLine();
        text.AppendLine("Usage:");
        text.AppendLine("  opus-alpha-host                        Open live D3D12 window (default).");
        text.AppendLine("  opus-alpha-host <asset>                Live window using the supplied glTF/GLB.");
        text.AppendLine("  opus-alpha-host smoke [options]        Headless N-frame smoke + smoke report.");
        text.AppendLine("  opus-alpha-host validate-package ...   Run M9 alpha package checklist.");
        text.AppendLine("  opus-alpha-host check-machine [...]    Capture / compare machine profile.");
        text.AppendLine("  opus-alpha-host soak [options]         Drive M8 loopback soak harness.");
        text.AppendLine("  opus-alpha-host stress [options]       Drive M11 alpha stress harness.");
        text.AppendLine("  opus-alpha-host known-issues-merge ... Merge two known-issue ledgers.");
        text.AppendLine("  opus-alpha-host known-issues-diff ...  Diff two known-issue ledgers.");
        text.AppendLine("  opus-alpha-host --help                 Print this help banner.");
        text.AppendLine();
        text.AppendLine("Common options (window / smoke):");
        text.AppendLine("  --scene <small|large|massive>  Scene-density preset (default: small).");
        text.AppendLine("  --asset <path>           glTF/GLB asset overriding the procedural fallback.");
        text.AppendLine("  --diagnostics-dir <dir>  Diagnostics root (logs / reports / smoke).");
        text.AppendLine("                           Overrides the OPUS_DIAGNOSTICS_DIR environment variable.");
        text.AppendLine("  --frame-budget           (window) Warn when the rolling frame window breaches the alpha pacing budget.");
        text.AppendLine("  --async-logging          (window) Write rolling logs off-thread so disk IO never stalls the frame loop.");
        text.AppendLine("  --overlay-level <off|minimal|full>  (window) Diagnostic overlay verbosity (default full; off for clean screenshots).");
        text.AppendLine("  --settings <path>        (window) Persisted tester-settings JSON (scene / overlay / frame-budget / async-logging); seeded with defaults when absent.");
        text.AppendLine("  --consumer <path>        (window / smoke) Load an external ConsumerIntegration assembly and drive it instead of the sample scene.");
        text.AppendLine();
        text.AppendLine("Window diagnostics self-check:");
        text.AppendLine("  --inject-failure <startup|content|device-lost>  Force a startup failure to confirm a diagnostics bundle is written.");
        text.AppendLine();
        text.AppendLine("Smoke options:");
        text.AppendLine("  --frames N               Frame count (default 60).");
        text.AppendLine("  --screenshot-frame N     Capture a PNG at the supplied frame index.");
        text.AppendLine("  --report <path>          Override the smoke report directory.");
        text.AppendLine();
        text.AppendLine("Validate-package options:");
        text.AppendLine("  --package <path>         Directory or manifest under validation (required).");
        text.AppendLine();
        text.AppendLine("Check-machine options:");
        text.AppendLine("  --reference <path>       Compare against a known-good profile JSON.");
        text.AppendLine("  --save <path>            Serialise the captured profile to disk (overwrites).");
        text.AppendLine();
        text.AppendLine("Soak options:");
        text.AppendLine("  --peers N                Peer cohort (default 4).");
        text.AppendLine("  --packets N              Packets per peer (default 64).");
        text.AppendLine("  --payload N              Payload body bytes (default 256).");
        text.AppendLine();
        text.AppendLine("Stress options:");
        text.AppendLine("  --iterations N           Iteration count (default 5; max 200).");
        text.AppendLine("  --frames N               Per-iteration frame count (default 60).");
        text.AppendLine("  --scene <small|large|massive>  Per-iteration scene-density preset.");
        text.AppendLine("  --stress-dir <dir>       Override the stress-report directory.");
        text.AppendLine("  --known-issues <path>    Load known-issue ledger JSON before the run.");
        text.AppendLine();
        text.AppendLine("Stress fault-injection options (any one enables M11.1 network probe):");
        text.AppendLine("  --inject-loss <0..1>     Outbound per-packet drop probability (default 0).");
        text.AppendLine("  --inject-latency-ms N    Outbound added send latency in milliseconds (default 0).");
        text.AppendLine("  --inject-seed N          Outbound deterministic RNG seed (default 20260527).");
        text.AppendLine("  --inject-peers N         Per-iteration peer cohort (default 4).");
        text.AppendLine("  --inject-packets N       Per-iteration packets per peer (default 32).");
        text.AppendLine("  --inject-payload N       Per-iteration payload bytes (default 256).");
        text.AppendLine("  --inject-drop-tolerance <0..1>  Max outbound drop fraction before OPDX-STR-006 (default 0.25).");
        text.AppendLine();
        text.AppendLine("Stress fault-injection options (M11.2 inbound shape):");
        text.AppendLine("  --inject-inbound-loss <0..1>            Inbound per-Received drop probability (default 0).");
        text.AppendLine("  --inject-inbound-latency-ms N           Inbound added latency in milliseconds (default 0).");
        text.AppendLine("  --inject-inbound-seed N                 Inbound deterministic RNG seed (default 20260528).");
        text.AppendLine("  --inject-inbound-drop-tolerance <0..1>  Max inbound drop fraction before OPDX-STR-006 (default 1.0 = disabled).");
        text.AppendLine();
        text.AppendLine("Known-issues-merge options:");
        text.AppendLine("  --base <path>            Base ledger JSON (required).");
        text.AppendLine("  --overlay <path>         Overlay ledger JSON; overlay records win on id collision (required).");
        text.AppendLine("  --output <path>          Merged ledger JSON destination (required, atomic temp+replace).");
        text.AppendLine();
        text.AppendLine("Known-issues-diff options:");
        text.AppendLine("  --left <path>            Left ledger JSON (required).");
        text.AppendLine("  --right <path>           Right ledger JSON (required).");
        text.AppendLine("  --format <text|json>     Output shape (default: text).");
        text.AppendLine("  --output <path>          Diff destination; omit to print to stdout (atomic temp+replace).");
        return text.ToString();
    }

    /// <summary>Writes the banner to the supplied writer; convenience for the dispatcher.</summary>
    public static void Write(TextWriter writer, string reason)
    {
        writer.Write(Render(reason));
    }
}
