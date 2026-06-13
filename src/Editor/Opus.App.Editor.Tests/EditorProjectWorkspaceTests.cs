using System;
using System.IO;
using FluentAssertions;
using Opus.App.Editor.Run;
using Opus.Editor.Core;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorProjectWorkspaceTests
{
    private static readonly string ProjectDirectory = Path.Combine(Path.GetTempPath(), "opus-proj");

    private static EditorProjectDocument Project(
        string[] contentRoots, string[] scenes) => new(
        "Campaign", contentRoots, scenes, Array.Empty<string>(), Array.Empty<string>());

    [Fact]
    public void Relative_references_resolve_against_the_project_directory()
    {
        var document = Project(new[] { "assets" }, new[] { Path.Combine("maps", "harbor.scene.json") });

        var workspace = EditorProjectWorkspace.Resolve(document, ProjectDirectory);

        workspace.Name.Should().Be("Campaign");
        workspace.ContentRoots.Should().Equal(Path.Combine(ProjectDirectory, "assets"));
        workspace.Scenes.Should().Equal(Path.Combine(ProjectDirectory, "maps", "harbor.scene.json"));
    }

    [Fact]
    public void Rooted_references_are_kept_as_they_are()
    {
        string rooted = Path.Combine(Path.GetTempPath(), "shared-assets");
        var document = Project(new[] { rooted }, Array.Empty<string>());

        var workspace = EditorProjectWorkspace.Resolve(document, ProjectDirectory);

        workspace.ContentRoots.Should().Equal(rooted);
    }

    [Fact]
    public void Blank_and_malformed_references_are_dropped()
    {
        var document = Project(new[] { " ", "assets", "bad\0root" }, Array.Empty<string>());

        var workspace = EditorProjectWorkspace.Resolve(document, ProjectDirectory);

        workspace.ContentRoots.Should().Equal(Path.Combine(ProjectDirectory, "assets"));
    }
}
