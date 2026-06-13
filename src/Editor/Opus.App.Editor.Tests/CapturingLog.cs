using System;
using System.Collections.Generic;
using Opus.Foundation;

namespace Opus.App.Editor.Tests;

/// <summary>An <see cref="ILog"/> that records every message, so runner output is asserted headlessly.</summary>
internal sealed class CapturingLog : ILog
{
    public List<string> Messages { get; } = new();

    public string Joined => string.Join("\n", Messages);

    public bool IsEnabled(LogLevel level) => true;

    public void Log(LogLevel level, string message, Exception? exception = null) => Messages.Add(message);
}
