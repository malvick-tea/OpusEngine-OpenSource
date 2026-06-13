using System;
using System.Collections.Generic;

namespace Opus.App.Editor.Cli;

/// <summary>
/// One editor CLI sub-command: its verb, the parser that turns its argument vector into an
/// <see cref="EditorArgs"/>, and the help usage lines it contributes. Pairing all three in one descriptor is
/// the single source of truth — a new command is added in exactly one place (the dispatch table in
/// <see cref="EditorCliParser"/>), so the parser and the help banner can never drift apart.
/// </summary>
/// <param name="Name">The command verb (args[0]).</param>
/// <param name="Parse">Parses the full argument vector for this command into an <see cref="EditorArgs"/>.</param>
/// <param name="UsageLines">The help-banner lines for this command, already formatted for the renderer.</param>
public sealed record EditorCommand(string Name, Func<string[], EditorArgs> Parse, IReadOnlyList<string> UsageLines);
