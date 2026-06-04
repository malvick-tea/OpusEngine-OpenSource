namespace Opus.App.OpusAlpha.Cli;

/// <summary>Output shape selected by the <c>known-issues-diff</c> sub-command.
/// <see cref="Text"/> is the default grep-friendly layout; <see cref="Json"/> emits
/// a structured payload tester-tooling can parse without depending on the engine.</summary>
public enum KnownIssuesDiffFormat
{
    /// <summary>Plain-text grep-friendly layout printed line-by-line.</summary>
    Text,

    /// <summary>Structured JSON payload with per-change records.</summary>
    Json,
}
