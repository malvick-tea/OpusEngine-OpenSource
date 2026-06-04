using System;

namespace Opus.Foundation;

/// <summary>
/// Minimal log abstraction so Foundation/Sim/Content code never takes a hard dependency
/// on Serilog or Microsoft.Extensions.Logging. Hosts (Client, Server, Tools) adapt their
/// preferred sink to this interface.
/// </summary>
public interface ILog
{
    bool IsEnabled(LogLevel level);

    void Log(LogLevel level, string message, Exception? exception = null);
}

public static class LogExtensions
{
    public static void Trace(this ILog log, string message) => log.Log(LogLevel.Trace, message);

    public static void Debug(this ILog log, string message) => log.Log(LogLevel.Debug, message);

    public static void Info(this ILog log, string message) => log.Log(LogLevel.Information, message);

    public static void Warn(this ILog log, string message) => log.Log(LogLevel.Warning, message);

    public static void Error(this ILog log, string message, Exception? ex = null) =>
        log.Log(LogLevel.Error, message, ex);

    public static void Critical(this ILog log, string message, Exception? ex = null) =>
        log.Log(LogLevel.Critical, message, ex);
}
