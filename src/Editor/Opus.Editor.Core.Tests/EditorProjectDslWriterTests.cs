using FluentAssertions;
using Xunit;

namespace Opus.Editor.Core.Tests;

public sealed class EditorProjectDslWriterTests
{
    [Fact]
    public void Writes_each_reference_kind()
    {
        var document = new EditorProjectDocument(
            "Campaign",
            new[] { "assets" },
            new[] { "harbor.scene.json" },
            new[] { "loco.animgraph.json" },
            new[] { "textures" });

        var text = EditorProjectDslWriter.Write(document);

        text.Should().Contain("project \"Campaign\" {");
        text.Should().Contain("content-root \"assets\"");
        text.Should().Contain("scene \"harbor.scene.json\"");
        text.Should().Contain("animgraph \"loco.animgraph.json\"");
        text.Should().Contain("material-root \"textures\"");
    }

    [Fact]
    public void Uses_newline_line_endings_only()
    {
        var text = EditorProjectDslWriter.Write(EditorProjectDocument.Empty("x"));

        text.Should().NotContain("\r");
    }
}
