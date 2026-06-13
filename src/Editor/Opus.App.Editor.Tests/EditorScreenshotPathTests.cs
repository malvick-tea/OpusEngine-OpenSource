using System;
using System.IO;
using FluentAssertions;
using Opus.App.Editor.Run;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorScreenshotPathTests
{
    private static readonly DateTimeOffset SampleTime =
        new(2026, 6, 6, 13, 5, 9, 123, TimeSpan.Zero);

    [Fact]
    public void File_name_is_timestamped_and_png()
    {
        EditorScreenshotPath.FileName(SampleTime).Should().Be("opus-editor-20260606-130509-123.png");
    }

    [Fact]
    public void Build_combines_the_directory_and_file_name()
    {
        EditorScreenshotPath.Build("shots", SampleTime).Should()
            .Be(Path.Combine("shots", "opus-editor-20260606-130509-123.png"));
    }

    [Fact]
    public void The_directory_is_the_named_subfolder_of_the_working_directory()
    {
        EditorScreenshotPath.Directory().Should()
            .Be(Path.Combine(Environment.CurrentDirectory, EditorScreenshotPath.DirectoryName));
    }
}
