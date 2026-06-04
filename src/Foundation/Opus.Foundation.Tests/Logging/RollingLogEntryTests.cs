using System;
using FluentAssertions;
using Opus.Foundation;
using Xunit;

namespace Opus.Foundation.Tests.Logging;

public sealed class RollingLogEntryTests
{
    [Fact]
    public void Create_normalises_timestamp_to_utc()
    {
        var local = new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.FromHours(3));

        var entry = RollingLogEntry.Create(local, LogLevel.Information, "x", exception: null);

        entry.TimestampUtc.Offset.Should().Be(TimeSpan.Zero);
        entry.TimestampUtc.Should().Be(local.ToUniversalTime());
    }

    [Fact]
    public void Create_escapes_embedded_newlines_in_message()
    {
        var entry = RollingLogEntry.Create(
            DateTimeOffset.UtcNow,
            LogLevel.Information,
            "first\r\nsecond\nthird",
            exception: null);

        entry.Message.Should().Be("first" + RollingLogEntry.NewlineEscape + "second"
            + RollingLogEntry.NewlineEscape + "third");
        entry.Message.Should().NotContain("\r");
        entry.Message.Should().NotContain("\n");
    }

    [Fact]
    public void Create_truncates_oversize_exception_message()
    {
        var longMessage = new string('x', RollingLogEntry.MaxExceptionMessageChars + 64);
        var entry = RollingLogEntry.Create(
            DateTimeOffset.UtcNow,
            LogLevel.Error,
            "captured",
            new InvalidOperationException(longMessage));

        entry.ExceptionMessage.Should().NotBeNull();
        entry.ExceptionMessage!.Length.Should().Be(RollingLogEntry.MaxExceptionMessageChars);
        entry.ExceptionMessage.Should().EndWith(RollingLogEntry.TruncationSuffix);
    }

    [Fact]
    public void To_display_line_is_a_single_line_even_with_multiline_input()
    {
        var entry = RollingLogEntry.Create(
            new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero),
            LogLevel.Information,
            "alpha\nbeta",
            exception: null);

        var line = entry.ToDisplayLine();

        line.Should().NotContain("\n");
        line.Should().Contain("alpha\\nbeta");
    }

    [Fact]
    public void To_exception_continuation_line_is_null_without_exception()
    {
        var entry = RollingLogEntry.Create(
            DateTimeOffset.UtcNow,
            LogLevel.Information,
            "no exception",
            exception: null);

        entry.ToExceptionContinuationLine().Should().BeNull();
    }

    [Fact]
    public void To_exception_continuation_line_renders_type_and_message()
    {
        var entry = RollingLogEntry.Create(
            DateTimeOffset.UtcNow,
            LogLevel.Error,
            "captured",
            new InvalidOperationException("boom"));

        entry.ToExceptionContinuationLine().Should().Be(
            "  -> System.InvalidOperationException: boom");
    }
}
