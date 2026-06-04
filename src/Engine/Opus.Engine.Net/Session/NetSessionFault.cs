using System;

namespace Opus.Engine.Net.Session;

/// <summary>
/// Immutable description of a fatal session fault. Surfaced through
/// <see cref="INetSession.LastFault"/> and the <see cref="NetSessionEventKind.TransportFault"/>
/// / <see cref="NetSessionEventKind.ReconnectExhausted"/> events. Carries a stable code,
/// a human-readable detail, the capture timestamp, and the originating exception type +
/// message when one is available.
/// </summary>
public sealed record NetSessionFault(
    NetSessionFaultCode Code,
    string Detail,
    DateTimeOffset CapturedAtUtc,
    string? ExceptionType,
    string? ExceptionMessage)
{
    /// <summary>Maximum stored characters for an inline exception message. Longer text is
    /// truncated with an ellipsis so the fault record does not grow unboundedly when a
    /// transport reports a megabyte-long underlying error.</summary>
    public const int MaxExceptionMessageChars = 512;

    private const string TruncationSuffix = "...";

    /// <summary>Captures a fault from a plain detail message.</summary>
    public static NetSessionFault FromDetail(
        NetSessionFaultCode code,
        string detail,
        DateTimeOffset capturedAtUtc) =>
        new(code, detail, capturedAtUtc.ToUniversalTime(), ExceptionType: null, ExceptionMessage: null);

    /// <summary>Captures a fault that originated from an exception. The exception type
    /// and message are flattened into stable fields so the record stays renderable in
    /// JSON, plain text, and overlay rows alike.</summary>
    public static NetSessionFault FromException(
        NetSessionFaultCode code,
        string detail,
        DateTimeOffset capturedAtUtc,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new NetSessionFault(
            code,
            detail,
            capturedAtUtc.ToUniversalTime(),
            ExceptionType: exception.GetType().FullName ?? exception.GetType().Name,
            ExceptionMessage: Truncate(exception.Message));
    }

    private static string Truncate(string message)
    {
        if (message.Length <= MaxExceptionMessageChars)
        {
            return message;
        }

        return string.Concat(message.AsSpan(0, MaxExceptionMessageChars - TruncationSuffix.Length), TruncationSuffix);
    }
}
