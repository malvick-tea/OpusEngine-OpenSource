using System.IO;
using FluentAssertions;
using Opus.App.Editor.Run;
using Opus.Editor.Core;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorSceneSaveTests
{
    private static EditorDocument DirtyDocumentWithOneNode()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        return document;
    }

    [Fact]
    public void Saving_writes_the_document_and_clears_the_dirty_flag()
    {
        using var temp = new TempDirectory();
        string path = temp.File("harbor.scene.json");
        var document = DirtyDocumentWithOneNode();

        bool saved = EditorSceneSave.Save(document, path, new CapturingLog());

        saved.Should().BeTrue();
        document.IsDirty.Should().BeFalse("a successful save marks the document saved");
        var reloaded = EditorSceneFileStore.Load(path);
        reloaded.IsOk.Should().BeTrue();
        reloaded.Unwrap().Nodes.Should().ContainSingle(n => n.Name == "alpha");
    }

    [Fact]
    public void Saving_without_a_scene_path_is_a_no_op_and_leaves_the_document_dirty()
    {
        var document = DirtyDocumentWithOneNode();

        bool saved = EditorSceneSave.Save(document, scenePath: null, new CapturingLog());

        saved.Should().BeFalse();
        document.IsDirty.Should().BeTrue("an untitled window has no file to save to");
    }

    [Fact]
    public void Saving_a_clean_document_writes_nothing()
    {
        using var temp = new TempDirectory();
        string path = temp.File("harbor.scene.json");

        bool saved = EditorSceneSave.Save(new EditorDocument("Harbor"), path, new CapturingLog());

        saved.Should().BeFalse();
        File.Exists(path).Should().BeFalse("a clean document has nothing to write");
    }

    [Fact]
    public void An_untitled_dirty_document_saves_to_the_default_untitled_file()
    {
        using var temp = new TempDirectory();
        var document = DirtyDocumentWithOneNode();

        string? path = EditorSceneSave.SaveResolvingUntitled(
            document, scenePath: null, temp.Root, File.Exists, new CapturingLog());

        path.Should().Be(Path.Combine(temp.Root, "untitled.scene.json"));
        File.Exists(path).Should().BeTrue();
        document.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void The_resolved_untitled_path_is_reused_for_the_rest_of_the_session()
    {
        using var temp = new TempDirectory();
        var document = DirtyDocumentWithOneNode();
        var log = new CapturingLog();

        string? first = EditorSceneSave.SaveResolvingUntitled(document, null, temp.Root, File.Exists, log);
        document.PlaceNode("bravo", null, EditorTransform.Identity);
        string? second = EditorSceneSave.SaveResolvingUntitled(document, first, temp.Root, File.Exists, log);

        second.Should().Be(first, "the session keeps the file it claimed on the first save");
        EditorSceneFileStore.Load(second!).Unwrap().Nodes.Should().HaveCount(2);
    }

    [Fact]
    public void A_taken_untitled_name_is_skipped_instead_of_overwritten()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.File("untitled.scene.json"), "{}");
        var document = DirtyDocumentWithOneNode();

        string? path = EditorSceneSave.SaveResolvingUntitled(
            document, scenePath: null, temp.Root, File.Exists, new CapturingLog());

        path.Should().Be(Path.Combine(temp.Root, "untitled-2.scene.json"));
        File.ReadAllText(temp.File("untitled.scene.json")).Should().Be("{}", "the earlier file is untouched");
    }

    [Fact]
    public void A_clean_untitled_document_claims_no_path_and_writes_nothing()
    {
        using var temp = new TempDirectory();

        string? path = EditorSceneSave.SaveResolvingUntitled(
            new EditorDocument("Harbor"), scenePath: null, temp.Root, File.Exists, new CapturingLog());

        path.Should().BeNull();
        Directory.GetFiles(temp.Root).Should().BeEmpty();
    }

    [Fact]
    public void A_successful_save_clears_the_recovery_sidecar()
    {
        using var temp = new TempDirectory();
        string path = temp.File("harbor.scene.json");
        File.WriteAllText(temp.File("harbor.autosave.scene.json"), "{}");

        EditorSceneSave.SaveResolvingUntitled(
            DirtyDocumentWithOneNode(), path, temp.Root, File.Exists, new CapturingLog());

        File.Exists(temp.File("harbor.autosave.scene.json")).Should().BeFalse(
            "the saved file supersedes the recovery copy");
    }

    [Fact]
    public void A_clean_document_no_op_keeps_an_existing_sidecar()
    {
        using var temp = new TempDirectory();
        string path = temp.File("harbor.scene.json");
        File.WriteAllText(temp.File("harbor.autosave.scene.json"), "{}");

        EditorSceneSave.SaveResolvingUntitled(
            new EditorDocument("Harbor"), path, temp.Root, File.Exists, new CapturingLog());

        File.Exists(temp.File("harbor.autosave.scene.json")).Should().BeTrue(
            "no write happened, so the sidecar may still hold unrecovered work");
    }

    [Fact]
    public void A_titled_scene_keeps_its_own_path()
    {
        using var temp = new TempDirectory();
        string path = temp.File("harbor.scene.json");
        var document = DirtyDocumentWithOneNode();

        string? kept = EditorSceneSave.SaveResolvingUntitled(
            document, path, temp.Root, File.Exists, new CapturingLog());

        kept.Should().Be(path);
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void Save_as_writes_the_named_file_and_moves_the_session_onto_it()
    {
        using var temp = new TempDirectory();
        var document = DirtyDocumentWithOneNode();

        string? path = EditorSceneSave.SaveAs(
            document, "Harbor-2", temp.Root, currentScenePath: null, new CapturingLog());

        path.Should().Be(temp.File("Harbor-2.scene.json"));
        document.IsDirty.Should().BeFalse();
        EditorSceneFileStore.Load(path!).Unwrap().Nodes.Should().ContainSingle(n => n.Name == "alpha");
    }

    [Fact]
    public void Save_as_keeps_a_name_already_carrying_the_extension()
    {
        using var temp = new TempDirectory();

        string? path = EditorSceneSave.SaveAs(
            new EditorDocument("Harbor"), "copy.scene.json", temp.Root, null, new CapturingLog());

        path.Should().Be(temp.File("copy.scene.json"), "the extension is not doubled");
        File.Exists(path).Should().BeTrue("a save-as writes even a clean document — it is an explicit copy");
    }

    [Fact]
    public void Save_as_refuses_to_overwrite_a_different_existing_file()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.File("other.scene.json"), "{}");

        string? path = EditorSceneSave.SaveAs(
            DirtyDocumentWithOneNode(), "other", temp.Root, temp.File("harbor.scene.json"), new CapturingLog());

        path.Should().BeNull();
        File.ReadAllText(temp.File("other.scene.json")).Should().Be("{}", "the existing scene is untouched");
    }

    [Fact]
    public void Save_as_onto_the_current_file_is_allowed()
    {
        using var temp = new TempDirectory();
        string current = temp.File("harbor.scene.json");
        File.WriteAllText(current, "{}");

        string? path = EditorSceneSave.SaveAs(
            DirtyDocumentWithOneNode(), "harbor", temp.Root, current, new CapturingLog());

        path.Should().Be(current, "re-saving the open file under its own name is a plain save");
    }

    [Fact]
    public void Save_as_with_a_blank_name_saves_nothing()
    {
        using var temp = new TempDirectory();

        string? path = EditorSceneSave.SaveAs(
            DirtyDocumentWithOneNode(), "   ", temp.Root, null, new CapturingLog());

        path.Should().BeNull();
        Directory.GetFiles(temp.Root).Should().BeEmpty();
    }
}
