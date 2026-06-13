using FluentAssertions;
using Opus.Foundation;
using Xunit;

namespace Opus.Editor.Core.Tests;

public sealed class AnimationGraphSerializerTests
{
    [Fact]
    public void Round_trips_a_graph_document()
    {
        var idle = new AnimationStateId(1);
        var walk = new AnimationStateId(2);
        var document = new AnimationGraphDocument(
            "Locomotion",
            idle,
            new[]
            {
                new AnimationState(idle, "Idle", "idle.glb", true, 1f),
                new AnimationState(walk, "Walk", "walk.glb", false, 1.25f),
            },
            new[] { new AnimationTransition(idle, walk, "move", 0.2f) });

        var json = AnimationGraphSerializer.Serialize(document);
        var result = AnimationGraphSerializer.Deserialize(json);

        result.IsOk.Should().BeTrue();
        result.Unwrap().Should().BeEquivalentTo(document);
    }

    [Fact]
    public void Malformed_json_is_a_typed_settings_corrupt_error()
    {
        var result = AnimationGraphSerializer.Deserialize("{ this is not json");

        result.IsErr.Should().BeTrue();
        result.UnwrapErr().Code.Should().Be(ErrorCode.SettingsCorrupt);
    }

    [Fact]
    public void Serialized_graph_is_human_readable_camel_case_json()
    {
        var document = AnimationGraphDocument.Empty("Locomotion");

        var json = AnimationGraphSerializer.Serialize(document);

        json.Should().Contain("\"name\": \"Locomotion\"");
        json.Should().Contain("entryState");
    }
}
