using System;
using System.IO;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Opus.Engine.Diagnostics;
using Opus.Engine.Diagnostics.Reports;
using Opus.Foundation;
using Xunit;

namespace Opus.Engine.Diagnostics.Tests.Reports;

public sealed class FailureReportWriterTests
{
    [Fact]
    public void Writer_creates_json_and_text_reports()
    {
        using var temp = TempDirectory.Create();
        var writer = new FailureReportWriter(new FailureReportWriterOptions(temp.Path));

        var result = writer.Write(NewReport());

        result.Succeeded.Should().BeTrue();
        File.Exists(result.JsonPath!).Should().BeTrue();
        File.Exists(result.TextPath!).Should().BeTrue();
        File.ReadAllText(result.JsonPath!).Should().Contain("StartupFailure");
        File.ReadAllText(result.TextPath!).Should().Contain("Opus failure report");
        File.ReadAllText(result.TextPath!).Should().Contain("last line");
    }

    [Fact]
    public void Writer_returns_structured_issue_for_invalid_directory()
    {
        var writer = new FailureReportWriter(new FailureReportWriterOptions(" "));

        var result = writer.Write(NewReport());

        result.Succeeded.Should().BeFalse();
        result.Issue.Should().NotBeNull();
        result.Issue!.Code.Should().Be(DiagnosticCodes.FailureReportConfigurationInvalid);
        result.Issue.RemediationHint.Should().Contain("diagnostics directory");
    }

    [Fact]
    public void Writer_emits_text_body_with_adapter_resolution_and_exception_chain()
    {
        using var temp = TempDirectory.Create();
        var writer = new FailureReportWriter(new FailureReportWriterOptions(temp.Path));
        var report = FailureReport.Capture(
            FailureReportKind.DeviceLost,
            new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero),
            BuildInfo.Current,
            FailureReportAdapterSnapshot.Create("Test Adapter", 1280, 720),
            new[] { "warm-up", "boom" },
            "capture.png",
            new InvalidOperationException("device lost"));

        var result = writer.Write(report);

        result.Succeeded.Should().BeTrue();
        var text = File.ReadAllText(result.TextPath!);
        text.Should().Contain("kind: DeviceLost");
        text.Should().Contain("adapter: Test Adapter");
        text.Should().Contain("resolution: 1280x720");
        text.Should().Contain("screenshot: capture.png");
        text.Should().Contain("device lost");
        text.Should().Contain("warm-up");
        text.Should().Contain("boom");
    }

    [Fact]
    public void Writer_emits_adapter_hardware_rows_when_identity_is_populated()
    {
        using var temp = TempDirectory.Create();
        var writer = new FailureReportWriter(new FailureReportWriterOptions(temp.Path));
        var report = FailureReport.Capture(
            FailureReportKind.DeviceLost,
            new DateTimeOffset(2026, 5, 29, 12, 0, 0, TimeSpan.Zero),
            BuildInfo.Current,
            FailureReportAdapterSnapshot.Create(
                "NVIDIA GeForce RTX 4090",
                1920,
                1080,
                DiagnosticAdapterHardware.Create(
                    "NVIDIA",
                    vendorId: 0x10DE,
                    deviceId: 0x2684,
                    dedicatedVideoMemoryBytes: 24L * 1024 * 1024 * 1024,
                    adapterClass: DiagnosticAdapterClass.Discrete)),
            Array.Empty<string>(),
            screenshotPath: null,
            new InvalidOperationException("device lost"));

        var result = writer.Write(report);

        result.Succeeded.Should().BeTrue();
        var text = File.ReadAllText(result.TextPath!);
        text.Should().Contain("adapterVendor: NVIDIA");
        text.Should().Contain("adapterVendorId: 0x10DE");
        text.Should().Contain("adapterDeviceId: 0x2684");
        text.Should().Contain("adapterClass: discrete");
        text.Should().Contain("adapterVramMb: 24576");

        var hardware = JsonDocument.Parse(File.ReadAllText(result.JsonPath!))
            .RootElement.GetProperty("Adapter").GetProperty("Hardware");
        hardware.GetProperty("VendorName").GetString().Should().Be("NVIDIA");
        hardware.GetProperty("DedicatedVideoMemoryBytes").GetInt64().Should().Be(24L * 1024 * 1024 * 1024);
    }

    [Fact]
    public void Writer_renders_missing_screenshot_as_none_label()
    {
        using var temp = TempDirectory.Create();
        var writer = new FailureReportWriter(new FailureReportWriterOptions(temp.Path));
        var report = FailureReport.Capture(
            FailureReportKind.StartupFailure,
            DateTimeOffset.UtcNow,
            BuildInfo.Current,
            FailureReportAdapterSnapshot.Unavailable,
            Array.Empty<string>(),
            screenshotPath: null,
            exception: null);

        var result = writer.Write(report);

        result.Succeeded.Should().BeTrue();
        File.ReadAllText(result.TextPath!).Should().Contain("screenshot: none");
    }

    [Fact]
    public void Writer_overwrites_existing_report_atomically()
    {
        using var temp = TempDirectory.Create();
        var writer = new FailureReportWriter(new FailureReportWriterOptions(temp.Path));

        var first = writer.Write(NewReport());
        File.WriteAllText(first.JsonPath!, "STALE", Encoding.UTF8);

        // Re-target by replicating the exact path so the writer hits the rename branch.
        File.Copy(first.JsonPath!, first.JsonPath! + ".sentinel");
        File.WriteAllText(first.JsonPath!, "{ \"corruption\": true }", Encoding.UTF8);

        var rewritten = writer.Write(NewReport());

        rewritten.Succeeded.Should().BeTrue();
        File.ReadAllText(rewritten.JsonPath!).Should().NotContain("corruption");
        Directory.GetFiles(temp.Path, "*.tmp").Should().BeEmpty(
            "atomic write must not leave behind temp files on success.");
    }

    [Fact]
    public void Writer_emits_machine_readable_json_with_all_top_level_fields()
    {
        using var temp = TempDirectory.Create();
        var writer = new FailureReportWriter(new FailureReportWriterOptions(temp.Path));

        var result = writer.Write(NewReport());

        var document = JsonDocument.Parse(File.ReadAllText(result.JsonPath!));
        document.RootElement.TryGetProperty("Kind", out _).Should().BeTrue();
        document.RootElement.TryGetProperty("CapturedAtUtc", out _).Should().BeTrue();
        document.RootElement.TryGetProperty("Adapter", out _).Should().BeTrue();
        document.RootElement.TryGetProperty("LastLogLines", out _).Should().BeTrue();
        document.RootElement.TryGetProperty("ExceptionChain", out _).Should().BeTrue();
    }

    [Fact]
    public void Writer_emits_consumer_section_with_supplied_lines()
    {
        using var temp = TempDirectory.Create();
        var writer = new FailureReportWriter(new FailureReportWriterOptions(temp.Path));
        var report = FailureReport.Capture(
            FailureReportKind.Crash,
            new DateTimeOffset(2026, 5, 29, 12, 0, 0, TimeSpan.Zero),
            BuildInfo.Current,
            FailureReportAdapterSnapshot.Unavailable,
            Array.Empty<string>(),
            screenshotPath: null,
            exception: null,
            consumerLines: new[] { "match=skirmish-7", "phase=briefing" });

        var result = writer.Write(report);

        result.Succeeded.Should().BeTrue();
        var text = File.ReadAllText(result.TextPath!);
        text.Should().Contain("consumer:");
        text.Should().Contain("match=skirmish-7");
        text.Should().Contain("phase=briefing");
    }

    [Fact]
    public void Writer_renders_empty_consumer_section_as_none_label()
    {
        using var temp = TempDirectory.Create();
        var writer = new FailureReportWriter(new FailureReportWriterOptions(temp.Path));

        var result = writer.Write(NewReport());

        result.Succeeded.Should().BeTrue();
        File.ReadAllText(result.TextPath!).Should().MatchRegex(@"consumer:\s+none");
    }

    [Fact]
    public void Writer_attaches_screenshot_next_to_bundle_when_source_exists()
    {
        using var temp = TempDirectory.Create();
        var sourcePath = Path.Combine(temp.Path, "capture-source.png");
        var pngBytes = new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 1, 2, 3, 4 };
        File.WriteAllBytes(sourcePath, pngBytes);
        var writer = new FailureReportWriter(new FailureReportWriterOptions(temp.Path));

        var result = writer.Write(NewReportWithScreenshot(sourcePath));

        result.Succeeded.Should().BeTrue();
        result.AttachedScreenshotPath.Should().NotBeNull();
        File.Exists(result.AttachedScreenshotPath!).Should().BeTrue();
        File.ReadAllBytes(result.AttachedScreenshotPath!).Should().Equal(pngBytes);
        Path.GetExtension(result.AttachedScreenshotPath!).Should().Be(".png");
        Path.GetFileNameWithoutExtension(result.AttachedScreenshotPath!)
            .Should().Be(
                Path.GetFileNameWithoutExtension(result.TextPath!),
                "the attached screenshot shares the report bundle stem so the three artifacts travel together.");
    }

    [Fact]
    public void Writer_does_not_attach_when_screenshot_path_is_null()
    {
        using var temp = TempDirectory.Create();
        var writer = new FailureReportWriter(new FailureReportWriterOptions(temp.Path));

        var result = writer.Write(NewReport());

        result.Succeeded.Should().BeTrue();
        result.AttachedScreenshotPath.Should().BeNull();
        Directory.GetFiles(temp.Path, "*.png").Should().BeEmpty();
    }

    [Fact]
    public void Writer_skips_attachment_when_screenshot_source_is_missing()
    {
        using var temp = TempDirectory.Create();
        var missingSource = Path.Combine(temp.Path, "never-captured.png");
        var writer = new FailureReportWriter(new FailureReportWriterOptions(temp.Path));

        var result = writer.Write(NewReportWithScreenshot(missingSource));

        result.Succeeded.Should().BeTrue("a stale screenshot path must not fail the report write.");
        result.AttachedScreenshotPath.Should().BeNull();
        File.ReadAllText(result.TextPath!).Should().Contain(
            "screenshot: " + missingSource,
            "the source path is still recorded as provenance even when the file is gone.");
    }

    [Fact]
    public void Writer_screenshot_attachment_leaves_no_temp_file()
    {
        using var temp = TempDirectory.Create();
        var sourcePath = Path.Combine(temp.Path, "capture-source.png");
        File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3 });
        var writer = new FailureReportWriter(new FailureReportWriterOptions(temp.Path));

        writer.Write(NewReportWithScreenshot(sourcePath));

        Directory.GetFiles(temp.Path, "*.tmp").Should().BeEmpty(
            "the screenshot copy must rename its temp into place on success.");
    }

    private static FailureReport NewReportWithScreenshot(string screenshotPath) => FailureReport.Capture(
        FailureReportKind.Crash,
        new DateTimeOffset(2026, 5, 29, 12, 0, 0, TimeSpan.Zero),
        BuildInfo.Current,
        FailureReportAdapterSnapshot.Unavailable,
        new[] { "last line" },
        screenshotPath,
        new InvalidOperationException("boom"));

    private static FailureReport NewReport() => FailureReport.Capture(
        FailureReportKind.StartupFailure,
        new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero),
        BuildInfo.Current,
        FailureReportAdapterSnapshot.Unavailable,
        new[] { "last line" },
        screenshotPath: null,
        new InvalidOperationException("boom"));

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
                "opus-failure-report-tests-" + Guid.NewGuid().ToString("N"));
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
