using FluentAssertions;
using Opus.Foundation;
using Opus.Persistence;
using Xunit;

namespace Opus.Persistence.Tests;

public sealed class HeaderTests
{
    [Fact]
    public void Save_header_has_magic_constant()
    {
        var h = SaveHeader.Current(1, AppVersion.Dev, unixMs: 0);
        h.IsRecognisedMagic.Should().BeTrue();
        h.Magic.Should().Be("GRPSAV1");
    }

    [Fact]
    public void Save_header_rejects_foreign_magic()
    {
        var h = new SaveHeader("OTHER", 1, AppVersion.Dev, 0);
        h.IsRecognisedMagic.Should().BeFalse();
    }

    [Fact]
    public void Replay_header_has_distinct_magic()
    {
        var h = ReplayHeader.Current(1, AppVersion.Dev, "mission_x", 42UL, 60, 0, "p1");
        h.Magic.Should().Be("GRPRPL1");
        h.Magic.Should().NotBe(SaveHeader.MagicV1, "save and replay magics must not collide");
    }

    [Fact]
    public void Save_slot_distinguishes_autosave_from_manual()
    {
        SaveSlot.Autosave.IsAutosave.Should().BeTrue();
        SaveSlot.Autosave.ToString().Should().Be("autosave");

        var slot3 = new SaveSlot(3);
        slot3.IsAutosave.Should().BeFalse();
        slot3.ToString().Should().Be("slot3");
    }
}
