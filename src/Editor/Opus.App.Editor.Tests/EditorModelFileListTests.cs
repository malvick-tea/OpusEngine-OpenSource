using System.IO;
using FluentAssertions;
using Opus.App.Editor.Run;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorModelFileListTests
{
    [Fact]
    public void Models_are_listed_recursively_as_sorted_relative_forward_slash_refs()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.File("models"));
        Directory.CreateDirectory(temp.File("props"));
        File.WriteAllText(temp.File(Path.Combine("models", "tank.glb")), string.Empty);
        File.WriteAllText(temp.File(Path.Combine("props", "crate.gltf")), string.Empty);
        File.WriteAllText(temp.File("ambient.glb"), string.Empty);

        var refs = EditorModelFileList.List(temp.Root);

        refs.Should().Equal("ambient.glb", "models/tank.glb", "props/crate.gltf");
    }

    [Fact]
    public void Non_model_files_are_excluded()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.File("tank.glb"), string.Empty);
        File.WriteAllText(temp.File("notes.txt"), string.Empty);
        File.WriteAllText(temp.File("scene.scene.json"), string.Empty);
        File.WriteAllText(temp.File("texture.png"), string.Empty);

        var refs = EditorModelFileList.List(temp.Root);

        refs.Should().Equal("tank.glb");
    }

    [Fact]
    public void The_extension_match_is_case_insensitive()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.File("TANK.GLB"), string.Empty);

        EditorModelFileList.List(temp.Root).Should().ContainSingle();
    }

    [Fact]
    public void A_missing_root_yields_an_empty_list_not_a_throw()
    {
        using var temp = new TempDirectory();
        string missing = temp.File("no-such-folder");

        EditorModelFileList.List(missing).Should().BeEmpty();
    }

    [Fact]
    public void Several_roots_union_their_refs_sorted_with_duplicates_taken_once()
    {
        using var first = new TempDirectory();
        using var second = new TempDirectory();
        File.WriteAllText(first.File("tank.glb"), string.Empty);
        File.WriteAllText(first.File("shared.glb"), string.Empty);
        File.WriteAllText(second.File("crate.glb"), string.Empty);
        File.WriteAllText(second.File("shared.glb"), string.Empty);

        var refs = EditorModelFileList.List(new[] { first.Root, second.Root });

        refs.Should().Equal("crate.glb", "shared.glb", "tank.glb");
    }

    [Fact]
    public void A_missing_root_among_several_contributes_nothing()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.File("tank.glb"), string.Empty);

        var refs = EditorModelFileList.List(new[] { temp.File("no-such-folder"), temp.Root });

        refs.Should().Equal("tank.glb");
    }
}
