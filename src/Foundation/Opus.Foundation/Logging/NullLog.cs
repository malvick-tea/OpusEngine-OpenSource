using System;

namespace Opus.Foundation;

/// <summary>Sink-of-last-resort. Useful for tests and as a default DI binding.</summary>
public sealed class NullLog : ILog
{
    public static readonly NullLog Instance = new();

    public bool IsEnabled(LogLevel level) => false;

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        // intentionally empty
    }
}
