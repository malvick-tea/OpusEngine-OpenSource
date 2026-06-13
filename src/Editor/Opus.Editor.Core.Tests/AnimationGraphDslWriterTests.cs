using FluentAssertions;
using Xunit;

namespace Opus.Editor.Core.Tests;

public sealed class AnimationGraphDslWriterTests
{
    private static AnimationGraphDocument Sample()
    {
        var idle = new AnimationStateId(1);
        var walk = new AnimationStateId(2);
        return new AnimationGraphDocument(
            "Locomotion",
            idle,
            new[]
            {
                new AnimationState(idle, "Idle", "idle.glb", true, 1f),
                new AnimationState(walk, "Walk", "walk.glb", true, 1.5f),
            },
            new[] { new AnimationTransition(idle, walk, "move", 0.2f) });
    }

    [Fact]
    public void Writes_header_entry_states_and_transitions_by_name()
    {
        var text = AnimationGraphDslWriter.Write(Sample());

        text.Should().Contain("animgraph \"Locomotion\" {");
        text.Should().Contain("entry \"Idle\"");
        text.Should().Contain("state \"Walk\" {");
        text.Should().Contain("clip \"idle.glb\"");
        text.Should().Contain("loop true");
        text.Should().Contain("speed 1.5");
        text.Should().Contain("transition \"Idle\" -> \"Walk\" on \"move\" blend 0.2");
    }

    [Fact]
    public void Is_deterministic()
    {
        var document = Sample();

        AnimationGraphDslWriter.Write(document).Should().Be(AnimationGraphDslWriter.Write(document));
    }

    [Fact]
    public void Uses_newline_line_endings_only()
    {
        var text = AnimationGraphDslWriter.Write(Sample());

        text.Should().NotContain("\r");
    }
}
