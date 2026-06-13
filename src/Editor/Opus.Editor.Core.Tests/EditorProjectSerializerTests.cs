using FluentAssertions;
using Opus.Foundation;
using Xunit;

namespace Opus.Editor.Core.Tests;

public sealed class EditorProjectSerializerTests
{
    [Fact]
    public void Round_trips_a_project_document()
    {
        var document = new EditorProjectDocument(
            "Campaign",
            new[] { "assets", "packs" },
            new[] { "a.scene.json" },
            new[] { "g.animgraph.json" },
            new[] { "textures" });

        var json = EditorProjectSerializer.Serialize(document);
        var result = EditorProjectSerializer.Deserialize(json);

        result.IsOk.Should().BeTrue();
        result.Unwrap().Should().BeEquivalentTo(document);
    }

    [Fact]
    public void Malformed_json_is_a_typed_settings_corrupt_error()
    {
        var result = EditorProjectSerializer.Deserialize("{ this is not json");

        result.IsErr.Should().BeTrue();
        result.UnwrapErr().Code.Should().Be(ErrorCode.SettingsCorrupt);
    }

    [Fact]
    public void Serialized_project_is_human_readable_camel_case_json()
    {
        var json = EditorProjectSerializer.Serialize(EditorProjectDocument.Empty("Campaign"));

        json.Should().Contain("\"name\": \"Campaign\"");
        json.Should().Contain("contentRoots");
    }
}
