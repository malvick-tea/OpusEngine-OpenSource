using System;
using FluentAssertions;
using Xunit;

namespace Opus.Editor.Core.Tests;

public sealed class EditorProjectValidatorTests
{
    [Fact]
    public void All_present_references_are_valid()
    {
        var document = new EditorProjectDocument(
            "demo", new[] { "assets" }, new[] { "s.scene.json" },
            Array.Empty<string>(), Array.Empty<string>());

        EditorProjectValidator.IsValid(document, _ => true).Should().BeTrue();
    }

    [Fact]
    public void Missing_references_are_reported_with_their_kind()
    {
        var document = new EditorProjectDocument(
            "demo", new[] { "assets" }, new[] { "ghost.scene.json" },
            Array.Empty<string>(), new[] { "tex" });
        bool Exists(string path) => path == "assets";

        var issues = EditorProjectValidator.Validate(document, Exists);

        issues.Should().Contain(i => i.Kind == EditorProjectEntryKind.Scene && i.Path == "ghost.scene.json");
        issues.Should().Contain(i => i.Kind == EditorProjectEntryKind.MaterialRoot && i.Path == "tex");
        issues.Should().NotContain(i => i.Kind == EditorProjectEntryKind.ContentRoot);
    }

    [Fact]
    public void Empty_project_is_valid()
    {
        EditorProjectValidator.IsValid(EditorProjectDocument.Empty("blank"), _ => false).Should().BeTrue();
    }
}
