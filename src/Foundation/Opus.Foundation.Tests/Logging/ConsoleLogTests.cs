using System;
using System.IO;
using FluentAssertions;
using Opus.Foundation;
using Xunit;

namespace Opus.Foundation.Tests.Logging;

public sealed class ConsoleLogTests
{
    private const string FrozenTimestamp = "[12:34:56.789";

    [Fact]
    public void Information_writes_to_out_with_timestamp_and_level_tag()
    {
        var (log, stdout, stderr) = NewLog(LogLevel.Information);

        log.Info("hello opus");

        stdout.ToString().Trim().Should().Be($"{FrozenTimestamp} INF] hello opus");
        stderr.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Warning_and_above_write_to_err_not_out()
    {
        var (log, stdout, stderr) = NewLog(LogLevel.Trace);

        log.Warn("careful");
        log.Error("broken");
        log.Critical("dying");

        stdout.ToString().Should().BeEmpty();
        stderr.ToString().Should().Contain("WRN] careful");
        stderr.ToString().Should().Contain("ERR] broken");
        stderr.ToString().Should().Contain("CRT] dying");
    }

    [Fact]
    public void Below_minimum_level_is_dropped()
    {
        var (log, stdout, stderr) = NewLog(LogLevel.Warning);

        log.Trace("nope");
        log.Debug("nope");
        log.Info("nope");
        log.Warn("kept");

        stdout.ToString().Should().BeEmpty();
        stderr.ToString().Trim().Should().Be($"{FrozenTimestamp} WRN] kept");
    }

    [Fact]
    public void IsEnabled_respects_minimum_and_treats_None_as_off()
    {
        var log = new ConsoleLog(LogLevel.Warning);

        log.IsEnabled(LogLevel.Warning).Should().BeTrue();
        log.IsEnabled(LogLevel.Error).Should().BeTrue();
        log.IsEnabled(LogLevel.Information).Should().BeFalse();
        log.IsEnabled(LogLevel.None).Should().BeFalse();
    }

    [Fact]
    public void Exception_payload_is_appended_with_type_and_message()
    {
        var (log, _, stderr) = NewLog(LogLevel.Trace);

        log.Error("upload failed", new InvalidOperationException("disk full"));

        stderr.ToString().Should().Contain("ERR] upload failed");
        stderr.ToString().Should().Contain("-> InvalidOperationException: disk full");
    }

    [Fact]
    public void Constructor_rejects_null_writers_and_clock()
    {
        Action withNullOut = () => _ = new ConsoleLog(LogLevel.Information, null!, TextWriter.Null, TimeProvider.System);
        Action withNullErr = () => _ = new ConsoleLog(LogLevel.Information, TextWriter.Null, null!, TimeProvider.System);
        Action withNullClock = () => _ = new ConsoleLog(LogLevel.Information, TextWriter.Null, TextWriter.Null, null!);

        withNullOut.Should().Throw<ArgumentNullException>();
        withNullErr.Should().Throw<ArgumentNullException>();
        withNullClock.Should().Throw<ArgumentNullException>();
    }

    private static (ConsoleLog Log, StringWriter Out, StringWriter Err) NewLog(LogLevel minimum)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var clock = new FrozenClock(new DateTimeOffset(2026, 5, 26, 12, 34, 56, 789, TimeSpan.Zero));
        return (new ConsoleLog(minimum, stdout, stderr, clock), stdout, stderr);
    }

    private sealed class FrozenClock : TimeProvider
    {
        private readonly DateTimeOffset _utc;

        public FrozenClock(DateTimeOffset utc)
        {
            _utc = utc;
        }

        public override DateTimeOffset GetUtcNow() => _utc;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
