using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Opus.Foundation;
using Xunit;

namespace Opus.Foundation.Tests.Logging;

public sealed class RollingFileLogSinkTests
{
    [Fact]
    public void Rolling_file_log_writes_build_header_entry_and_tail()
    {
        using var temp = TempDirectory.Create();
        using var sink = new RollingFileLogSink(NewOptions(temp.Path), FrozenClock.Instance);

        sink.Info("host started");
        sink.Warn("watch this");

        ReadShared(sink.CurrentLogFilePath).Should().Contain(BuildInfo.Current.Engine.DisplayName);
        ReadShared(sink.CurrentLogFilePath).Should().Contain("host started");
        sink.SnapshotTail(8).Should().HaveCount(2);
        sink.SnapshotTail(1).Single().Message.Should().Be("watch this");
    }

    [Fact]
    public void Rolling_file_log_filters_below_minimum_level()
    {
        using var temp = TempDirectory.Create();
        using var sink = new RollingFileLogSink(
            NewOptions(temp.Path) with { MinimumLevel = LogLevel.Warning },
            FrozenClock.Instance);

        sink.Info("hidden");
        sink.Error("visible");

        var text = ReadShared(sink.CurrentLogFilePath);
        text.Should().NotContain("hidden");
        text.Should().Contain("visible");
        sink.SnapshotTail(8).Should().ContainSingle(entry => entry.Message == "visible");
    }

    [Fact]
    public void Rolling_file_log_rotates_by_real_stream_bytes()
    {
        using var temp = TempDirectory.Create();
        using var sink = new RollingFileLogSink(NewOptions(temp.Path), FrozenClock.Instance);

        for (var i = 0; i < 40; i++)
        {
            sink.Info("message-" + i + "-" + new string('x', 220));
        }

        Directory.GetFiles(temp.Path, "*.log").Length.Should().BeGreaterThan(1);
    }

    [Fact]
    public void Rolling_file_log_options_reject_invalid_values()
    {
        var options = NewOptions(" ") with { MaxTailEntries = 0 };

        var act = () => options.Validate();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rolling_file_log_escapes_embedded_newlines_in_message()
    {
        using var temp = TempDirectory.Create();
        using var sink = new RollingFileLogSink(NewOptions(temp.Path), FrozenClock.Instance);

        sink.Info("first line\nsecond line\r\nthird line");

        var text = ReadShared(sink.CurrentLogFilePath);
        text.Should().Contain("first line\\nsecond line\\nthird line");
        text.Split(Environment.NewLine)
            .Count(line => line.Contains("third line"))
            .Should().Be(1, "an embedded newline must not turn one log entry into multiple file lines.");
    }

    [Fact]
    public void Rolling_file_log_writes_exception_continuation_line()
    {
        using var temp = TempDirectory.Create();
        using var sink = new RollingFileLogSink(NewOptions(temp.Path), FrozenClock.Instance);

        sink.Error("captured", new InvalidOperationException("boom"));

        var text = ReadShared(sink.CurrentLogFilePath);
        text.Should().Contain("captured");
        text.Should().Contain("InvalidOperationException");
        text.Should().Contain("-> System.InvalidOperationException: boom");
    }

    [Fact]
    public void Rolling_file_log_handles_session_stamp_collision()
    {
        using var temp = TempDirectory.Create();
        using var first = new RollingFileLogSink(NewOptions(temp.Path), FrozenClock.Instance);
        using var second = new RollingFileLogSink(NewOptions(temp.Path), FrozenClock.Instance);

        first.CurrentLogFilePath.Should().NotBe(second.CurrentLogFilePath);
        File.Exists(first.CurrentLogFilePath).Should().BeTrue();
        File.Exists(second.CurrentLogFilePath).Should().BeTrue();
    }

    [Fact]
    public void Rolling_file_log_becomes_no_op_after_dispose()
    {
        using var temp = TempDirectory.Create();
        var sink = new RollingFileLogSink(NewOptions(temp.Path), FrozenClock.Instance);
        sink.Info("before dispose");
        sink.Dispose();

        var act = () => sink.Info("after dispose");

        act.Should().NotThrow("disposed rolling sink must accept logs as silent no-ops.");
    }

    [Fact]
    public void Rolling_file_log_options_reject_reserved_windows_names()
    {
        var options = NewOptions(Path.GetTempPath()) with { FileNamePrefix = "CON" };

        var act = () => options.Validate();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rolling_file_log_options_reject_minimum_level_none()
    {
        var options = NewOptions(Path.GetTempPath()) with { MinimumLevel = LogLevel.None };

        var act = () => options.Validate();

        act.Should().Throw<ArgumentException>();
    }

    private static RollingLogSinkOptions NewOptions(string directory) =>
        RollingLogSinkOptions.ForDirectory(directory) with
        {
            FileNamePrefix = "test-opus",
            MaxFileBytes = 4_096,
            MaxTailEntries = 4,
        };

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class FrozenClock : TimeProvider
    {
        public static FrozenClock Instance { get; } = new();

        public override DateTimeOffset GetUtcNow() =>
            new(2026, 5, 26, 12, 0, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "opus-rolling-log-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
