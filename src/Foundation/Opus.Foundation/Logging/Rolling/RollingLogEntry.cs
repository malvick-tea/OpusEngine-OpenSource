using System;
using System.Globalization;
using System.Text;

namespace Opus.Foundation;

/// <summary>
/// Immutable entry retained by <see cref="IRollingLogSink"/> for failure reports and
/// tester diagnostics. Message and exception text are normalised at construction time so
/// the rendered line is always one log line — embedded newlines and oversized exception
/// messages cannot corrupt the file or starve the in-memory tail.
/// </summary>
public sealed record RollingLogEntry(
    DateTimeOffset TimestampUtc,
    LogLevel Level,
    string Message,
    string? ExceptionType,
    string? ExceptionMessage)
{
    /// <summary>Maximum characters kept from an exception message before truncation. Long
    /// messages still appear in the tail but cannot dominate memory.</summary>
    public const int MaxExceptionMessageChars = 512;

    /// <summary>Truncation suffix appended when <see cref="ExceptionMessage"/> is cut.</summary>
    public const string TruncationSuffix = "...";

    /// <summary>Token substituted for embedded line breaks so a single entry is always
    /// rendered as one log line.</summary>
    public const string NewlineEscape = "\\n";

    /// <summary>Creates an entry from an <see cref="ILog.Log"/> call. Normalises the
    /// timestamp to UTC, escapes embedded newlines in <paramref name="message"/>, and
    /// truncates an oversized exception message.</summary>
    public static RollingLogEntry Create(
        DateTimeOffset timestampUtc,
        LogLevel level,
        string message,
        Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(message);
        var exceptionType = exception?.GetType().FullName ?? exception?.GetType().Name;
        return new RollingLogEntry(
            timestampUtc.ToUniversalTime(),
            level,
            EscapeLineBreaks(message),
            exceptionType,
            TruncateException(exception?.Message));
    }

    /// <summary>Formats the entry as one stable human-readable log line.</summary>
    public string ToDisplayLine()
    {
        var tag = RollingLogLevelTags.ToTag(Level);
        if (ExceptionType is null)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"[{TimestampUtc:O} {tag}] {Message}");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"[{TimestampUtc:O} {tag}] {Message} -> {ExceptionType}: {ExceptionMessage}");
    }

    /// <summary>Renders the trailing exception detail as its own continuation line. Returns
    /// <c>null</c> when the entry has no exception so callers can decide whether to write
    /// a follow-up line.</summary>
    public string? ToExceptionContinuationLine()
    {
        if (ExceptionType is null)
        {
            return null;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"  -> {ExceptionType}: {ExceptionMessage}");
    }

    private static string EscapeLineBreaks(string value)
    {
        if (value.IndexOfAny(LineBreakChars) < 0)
        {
            return value;
        }

        var buffer = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch == '\r')
            {
                buffer.Append(NewlineEscape);
                if (i + 1 < value.Length && value[i + 1] == '\n')
                {
                    i++;
                }

                continue;
            }

            if (ch == '\n')
            {
                buffer.Append(NewlineEscape);
                continue;
            }

            buffer.Append(ch);
        }

        return buffer.ToString();
    }

    private static string? TruncateException(string? exceptionMessage)
    {
        if (exceptionMessage is null)
        {
            return null;
        }

        var escaped = EscapeLineBreaks(exceptionMessage);
        if (escaped.Length <= MaxExceptionMessageChars)
        {
            return escaped;
        }

        var keep = MaxExceptionMessageChars - TruncationSuffix.Length;
        return string.Concat(escaped.AsSpan(0, keep), TruncationSuffix);
    }

    private static readonly char[] LineBreakChars = { '\r', '\n' };
}
