using FluentAssertions;
using Opus.App.Editor.Run;
using Opus.Editor.Core;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorWindowTitleTests
{
    [Fact]
    public void A_clean_document_titles_the_window_with_its_name()
    {
        EditorWindowTitle.For(new EditorDocument("Harbor")).Should().Be("Opus Editor — Harbor");
    }

    [Fact]
    public void Unsaved_edits_append_the_dirty_marker()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNewPrimitive(ScenePrimitiveKind.Cube, EditorTransform.Identity);

        EditorWindowTitle.For(document).Should().Be("Opus Editor — Harbor *");

        document.MarkSaved();
        EditorWindowTitle.For(document).Should().Be("Opus Editor — Harbor");
    }
}
