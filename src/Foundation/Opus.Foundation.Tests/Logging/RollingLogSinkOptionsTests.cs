using System;
using System.IO;
using FluentAssertions;
using Opus.Foundation;
using Xunit;

namespace Opus.Foundation.Tests.Logging;

public sealed class RollingLogSinkOptionsTests
{
    [Fact]
    public void Defaults_for_directory_produce_alpha_baseline_values()
    {
        var options = RollingLogSinkOptions.ForDirectory(Path.GetTempPath());

        options.DirectoryPath.Should().Be(Path.GetTempPath());
        options.FileNamePrefix.Should().Be(RollingLogSinkOptions.DefaultFileNamePrefix);
        options.MinimumLevel.Should().Be(LogLevel.Information);
        options.MaxFileBytes.Should().Be(RollingLogSinkOptions.DefaultMaxFileBytes);
        options.MaxTailEntries.Should().Be(RollingLogSinkOptions.DefaultMaxTailEntries);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_rejects_empty_directory(string directory)
    {
        var options = RollingLogSinkOptions.ForDirectory(Path.GetTempPath()) with
        {
            DirectoryPath = directory,
        };

        var act = () => options.Validate();

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_rejects_empty_file_name_prefix(string prefix)
    {
        var options = RollingLogSinkOptions.ForDirectory(Path.GetTempPath()) with { FileNamePrefix = prefix };

        var act = () => options.Validate();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Validate_rejects_file_name_prefix_with_invalid_chars()
    {
        var invalidChar = Path.GetInvalidFileNameChars()[0];
        var options = RollingLogSinkOptions.ForDirectory(Path.GetTempPath()) with
        {
            FileNamePrefix = "bad" + invalidChar + "name",
        };

        var act = () => options.Validate();

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("con")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("LPT3")]
    public void Validate_rejects_reserved_windows_device_names(string reserved)
    {
        var options = RollingLogSinkOptions.ForDirectory(Path.GetTempPath()) with
        {
            FileNamePrefix = reserved,
        };

        var act = () => options.Validate();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Validate_rejects_minimum_level_none()
    {
        var options = RollingLogSinkOptions.ForDirectory(Path.GetTempPath()) with
        {
            MinimumLevel = LogLevel.None,
        };

        var act = () => options.Validate();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Validate_rejects_max_file_bytes_below_minimum()
    {
        var options = RollingLogSinkOptions.ForDirectory(Path.GetTempPath()) with
        {
            MaxFileBytes = RollingLogSinkOptions.MinimumMaxFileBytes - 1,
        };

        var act = () => options.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Validate_rejects_max_tail_entries_below_minimum()
    {
        var options = RollingLogSinkOptions.ForDirectory(Path.GetTempPath()) with
        {
            MaxTailEntries = 0,
        };

        var act = () => options.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
