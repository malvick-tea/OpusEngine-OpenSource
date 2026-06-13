using System.IO;
using System.Linq;
using FluentAssertions;
using Opus.App.Editor.Run;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorSceneFileListTests
{
    [Fact]
    public void Lists_scene_files_sorted_by_name_and_ignores_other_files()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.File("bravo.scene.json"), "{}");
        File.WriteAllText(temp.File("alpha.scene.json"), "{}");
        File.WriteAllText(temp.File("notes.txt"), "x");
        File.WriteAllText(temp.File("model.glb"), "x");

        var files = EditorSceneFileList.List(temp.Root, currentScenePath: null);

        files.Select(Path.GetFileName).Should().Equal("alpha.scene.json", "bravo.scene.json");
    }

    [Fact]
    public void Includes_the_current_scenes_directory_when_it_differs()
    {
        using var working = new TempDirectory();
        using var elsewhere = new TempDirectory();
        File.WriteAllText(working.File("local.scene.json"), "{}");
        string current = elsewhere.File("remote.scene.json");
        File.WriteAllText(current, "{}");

        var files = EditorSceneFileList.List(working.Root, current);

        files.Select(Path.GetFileName).Should().Equal("local.scene.json", "remote.scene.json");
    }

    [Fact]
    public void A_missing_directory_yields_an_empty_list_instead_of_throwing()
    {
        using var temp = new TempDirectory();
        string missing = Path.Combine(temp.Root, "missing-subdir");

        var files = EditorSceneFileList.List(missing, currentScenePath: null);

        files.Should().BeEmpty();
    }

    [Fact]
    public void Project_scenes_lead_the_listing_with_missing_ones_skipped()
    {
        using var working = new TempDirectory();
        using var elsewhere = new TempDirectory();
        File.WriteAllText(working.File("local.scene.json"), "{}");
        string projectScene = elsewhere.File("harbor.scene.json");
        File.WriteAllText(projectScene, "{}");
        string missingScene = elsewhere.File("gone.scene.json");

        var files = EditorSceneFileList.List(
            working.Root, currentScenePath: null, new[] { projectScene, missingScene });

        files.Select(Path.GetFileName).Should().Equal("harbor.scene.json", "local.scene.json");
    }

    [Fact]
    public void A_project_scene_in_the_working_directory_is_listed_once()
    {
        using var working = new TempDirectory();
        string projectScene = working.File("harbor.scene.json");
        File.WriteAllText(projectScene, "{}");

        var files = EditorSceneFileList.List(working.Root, currentScenePath: null, new[] { projectScene });

        files.Should().HaveCount(1, "the project reference and the directory scan name the same file");
    }
}
