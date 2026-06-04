using FluentAssertions;
using Opus.Engine.Pal.Application;
using Xunit;

namespace Opus.Engine.Pal.Tests.Application;

public sealed class WindowOptionsTests
{
    [Fact]
    public void Default_uses_720p_with_vsync_on()
    {
        var opts = WindowOptions.Default("Opus");

        opts.Title.Should().Be("Opus");
        opts.Width.Should().Be(1280);
        opts.Height.Should().Be(720);
        opts.Resizable.Should().BeTrue();
        opts.VSync.Should().BeTrue();
        opts.Mode.Should().Be(WindowMode.Windowed);
    }

    [Fact]
    public void Records_are_value_equal()
    {
        var a = new WindowOptions("X", 800, 600, false, false, WindowMode.BorderlessFullscreen);
        var b = new WindowOptions("X", 800, 600, false, false, WindowMode.BorderlessFullscreen);

        a.Should().Be(b);
    }
}
