using System;
using System.Collections.Generic;
using Opus.Foundation;

namespace Opus.Engine.Net.Tests.Support;

/// <summary>Minimal in-memory <see cref="ILog"/> that captures every accepted entry so
/// tests can assert on shape and level without spinning up a Foundation rolling sink.</summary>
internal sealed class CollectingLog : ILog
{
    public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;

    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

    public bool IsEnabled(LogLevel level) => level >= MinimumLevel && level != LogLevel.None;

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        if (!IsEnabled(level))
        {
            return;
        }

        Entries.Add((level, message, exception));
    }
}
