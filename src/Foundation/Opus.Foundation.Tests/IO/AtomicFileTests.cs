using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Opus.Foundation.IO;
using Xunit;

namespace Opus.Foundation.Tests.IO;

public sealed class AtomicFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "opus-atomic-file-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void WriteAllText_replaces_destination_without_leaving_staging_files()
    {
        Directory.CreateDirectory(_directory);
        var destination = Path.Combine(_directory, "settings.json");
        File.WriteAllText(destination, "old");

        AtomicFile.WriteAllText(destination, "new");

        File.ReadAllText(destination).Should().Be("new");
        Directory.EnumerateFiles(_directory).Select(Path.GetFileName)
            .Should().Equal("settings.json");
    }

    [Fact]
    public void Copy_replaces_destination_without_leaving_staging_files()
    {
        Directory.CreateDirectory(_directory);
        var source = Path.Combine(_directory, "source.png");
        var destination = Path.Combine(_directory, "destination.png");
        File.WriteAllBytes(source, [1, 2, 3, 4]);
        File.WriteAllBytes(destination, [9]);

        AtomicFile.Copy(source, destination);

        File.ReadAllBytes(destination).Should().Equal(1, 2, 3, 4);
        Directory.EnumerateFiles(_directory).Select(Path.GetFileName)
            .Should().BeEquivalentTo(["source.png", "destination.png"]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
