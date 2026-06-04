using System;
using System.Collections.Generic;
using FluentAssertions;
using Opus.App.OpusAlpha.Cli;
using Opus.App.OpusAlpha.Run;
using Opus.Foundation;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Run;

public sealed class AlphaFailureInjectionTests
{
    [Fact]
    public void None_maps_to_no_exception()
    {
        AlphaFailureInjection.CreateException(AlphaFaultKind.None).Should().BeNull();
    }

    [Fact]
    public void Startup_maps_to_a_startup_exception()
    {
        AlphaFailureInjection.CreateException(AlphaFaultKind.Startup)
            .Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void Content_maps_to_engine_content_exception_with_path()
    {
        AlphaFailureInjection.CreateException(AlphaFaultKind.Content)
            .Should().BeOfType<EngineContentException>()
            .Which.ContentPath.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void DeviceLost_maps_to_engine_device_lost_exception_with_known_reason()
    {
        AlphaFailureInjection.CreateException(AlphaFaultKind.DeviceLost)
            .Should().BeOfType<EngineDeviceLostException>()
            .Which.DeviceRemovedReason.Should().NotBe(EngineDeviceLostException.UnknownDeviceRemovedReason);
    }

    [Fact]
    public void ThrowIfRequested_is_a_noop_for_none()
    {
        var log = new CapturingLog();

        var act = () => AlphaFailureInjection.ThrowIfRequested(AlphaFaultKind.None, log);

        act.Should().NotThrow();
        log.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void ThrowIfRequested_throws_and_logs_for_requested_kind()
    {
        var log = new CapturingLog();

        var act = () => AlphaFailureInjection.ThrowIfRequested(AlphaFaultKind.Content, log);

        act.Should().Throw<EngineContentException>();
        log.Warnings.Should().ContainMatch("*Content*");
    }

    private sealed class CapturingLog : ILog
    {
        public List<string> Warnings { get; } = new();

        public bool IsEnabled(LogLevel level) => true;

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            if (level == LogLevel.Warning)
            {
                Warnings.Add(message);
            }
        }
    }
}
