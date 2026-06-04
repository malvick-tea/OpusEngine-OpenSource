using System;
using FluentAssertions;
using Opus.Engine.Host.Windows.Direct3D12.Frame;
using Opus.Engine.Renderer.Direct3D12.Alpha;
using Xunit;

namespace Opus.Engine.Host.Windows.Direct3D12.Tests.Frame;

public sealed class D3D12AlphaFrameMetricsTests
{
    [Fact]
    public void Empty_snapshot_returns_zero_window()
    {
        var metrics = new D3D12AlphaFrameMetrics(windowSize: 16);

        var snapshot = metrics.Snapshot();

        snapshot.Should().Be(D3D12AlphaFrameMetricsSnapshot.Empty);
        snapshot.SampleCount.Should().Be(0);
        snapshot.TotalFramesObserved.Should().Be(0);
    }

    [Fact]
    public void Single_sample_snapshot_returns_that_sample_as_mean_min_max_p95()
    {
        var metrics = new D3D12AlphaFrameMetrics(windowSize: 16);
        var sample = TimeSpan.FromMilliseconds(7.5);

        metrics.Record(sample);
        var snapshot = metrics.Snapshot();

        snapshot.SampleCount.Should().Be(1);
        snapshot.TotalFramesObserved.Should().Be(1);
        snapshot.Mean.Should().Be(sample);
        snapshot.Min.Should().Be(sample);
        snapshot.Max.Should().Be(sample);
        snapshot.P95.Should().Be(sample);
    }

    [Fact]
    public void Window_caps_sample_count_but_total_frames_keeps_climbing()
    {
        var metrics = new D3D12AlphaFrameMetrics(windowSize: 4);
        for (var i = 1; i <= 10; i++)
        {
            metrics.Record(TimeSpan.FromMilliseconds(i));
        }

        var snapshot = metrics.Snapshot();

        snapshot.SampleCount.Should().Be(4);
        snapshot.TotalFramesObserved.Should().Be(10);
        snapshot.Min.Should().Be(TimeSpan.FromMilliseconds(7));
        snapshot.Max.Should().Be(TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public void Mean_is_arithmetic_average_of_window()
    {
        var metrics = new D3D12AlphaFrameMetrics(windowSize: 4);
        metrics.Record(TimeSpan.FromMilliseconds(10));
        metrics.Record(TimeSpan.FromMilliseconds(20));
        metrics.Record(TimeSpan.FromMilliseconds(30));
        metrics.Record(TimeSpan.FromMilliseconds(40));

        var snapshot = metrics.Snapshot();

        snapshot.Mean.Should().Be(TimeSpan.FromMilliseconds(25));
    }

    [Fact]
    public void P95_uses_nearest_rank_index()
    {
        var metrics = new D3D12AlphaFrameMetrics(windowSize: 100);
        for (var i = 1; i <= 100; i++)
        {
            metrics.Record(TimeSpan.FromMilliseconds(i));
        }

        var snapshot = metrics.Snapshot();

        snapshot.P95.Should().Be(TimeSpan.FromMilliseconds(95));
        snapshot.Max.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void Diagnostics_overload_records_cpu_frame_time_only()
    {
        var metrics = new D3D12AlphaFrameMetrics(windowSize: 8);
        var diagnostics = new D3D12AlphaFrameDiagnostics(
            AdapterName: "test",
            BackBufferWidth: 1280,
            BackBufferHeight: 720,
            SceneViewportWidth: 1256,
            SceneViewportHeight: 670,
            SubmittedDrawItems: 84,
            MapInstanceCount: 80,
            CpuFrameTime: TimeSpan.FromMilliseconds(4.2));

        metrics.Record(diagnostics);
        var snapshot = metrics.Snapshot();

        snapshot.SampleCount.Should().Be(1);
        snapshot.Mean.Should().Be(TimeSpan.FromMilliseconds(4.2));
    }

    [Fact]
    public void Window_size_must_be_positive()
    {
        var act = () => _ = new D3D12AlphaFrameMetrics(windowSize: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Negative_frame_time_is_rejected()
    {
        var metrics = new D3D12AlphaFrameMetrics(windowSize: 4);

        var act = () => metrics.Record(TimeSpan.FromMilliseconds(-1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
