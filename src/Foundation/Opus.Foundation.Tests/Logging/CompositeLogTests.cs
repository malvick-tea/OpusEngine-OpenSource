using System;
using System.Collections.Generic;
using FluentAssertions;
using Opus.Foundation;
using Xunit;

namespace Opus.Foundation.Tests.Logging;

public sealed class CompositeLogTests
{
    [Fact]
    public void Composite_log_fans_out_to_enabled_sinks()
    {
        var first = new CapturingLog(LogLevel.Information);
        var second = new CapturingLog(LogLevel.Warning);
        using var composite = CompositeLog.Create(first, second);

        composite.Info("hello");
        composite.Warn("careful");

        first.Messages.Should().Equal("hello", "careful");
        second.Messages.Should().Equal("careful");
    }

    [Fact]
    public void Composite_log_rejects_empty_sink_list()
    {
        var act = () => _ = new CompositeLog(Array.Empty<ILog>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Composite_log_rejects_null_sink_in_list()
    {
        var act = () => _ = new CompositeLog(new ILog[] { new CapturingLog(LogLevel.Trace), null! });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Composite_log_isolates_per_sink_failures()
    {
        var faulty = new FaultingLog();
        var healthy = new CapturingLog(LogLevel.Information);
        using var composite = CompositeLog.Create(faulty, healthy);

        composite.Info("survive");

        faulty.LogCallCount.Should().Be(1);
        healthy.Messages.Should().Equal("survive");
    }

    [Fact]
    public void Composite_log_becomes_no_op_after_dispose()
    {
        var captured = new CapturingLog(LogLevel.Trace);
        var composite = CompositeLog.Create(captured);

        composite.Dispose();
        composite.Info("after dispose");

        captured.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Composite_log_disposes_sinks_in_reverse_order()
    {
        var observed = new List<string>();
        var first = new ObservableSink("first", observed);
        var second = new ObservableSink("second", observed);
        var composite = CompositeLog.Create(first, second);

        composite.Dispose();

        observed.Should().Equal("second", "first");
    }

    [Fact]
    public void Composite_log_is_enabled_when_any_sink_is_enabled_below_disposal()
    {
        var trace = new CapturingLog(LogLevel.Trace);
        var critical = new CapturingLog(LogLevel.Critical);
        using var composite = CompositeLog.Create(trace, critical);

        composite.IsEnabled(LogLevel.Debug).Should().BeTrue();
        composite.IsEnabled(LogLevel.None).Should().BeFalse();
    }

    private sealed class CapturingLog : ILog
    {
        private readonly LogLevel _minimumLevel;
        private readonly List<string> _messages = new();

        public CapturingLog(LogLevel minimumLevel)
        {
            _minimumLevel = minimumLevel;
        }

        public IReadOnlyList<string> Messages => _messages;

        public bool IsEnabled(LogLevel level) =>
            level != LogLevel.None && level >= _minimumLevel;

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            if (IsEnabled(level))
            {
                _messages.Add(message);
            }
        }
    }

    private sealed class FaultingLog : ILog
    {
        public int LogCallCount { get; private set; }

        public bool IsEnabled(LogLevel level) => level != LogLevel.None;

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            LogCallCount++;
            throw new InvalidOperationException("sink intentionally broken");
        }
    }

    private sealed class ObservableSink : ILog, IDisposable
    {
        private readonly string _name;
        private readonly List<string> _observed;

        public ObservableSink(string name, List<string> observed)
        {
            _name = name;
            _observed = observed;
        }

        public bool IsEnabled(LogLevel level) => level != LogLevel.None;

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
        }

        public void Dispose() => _observed.Add(_name);
    }
}
