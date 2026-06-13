using System.IO;
using FluentAssertions;
using Opus.App.Editor.Run;
using Opus.Editor.Core;
using Opus.Foundation;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorSceneFileStoreTests
{
    [Fact]
    public void Save_then_load_round_trips()
    {
        using var temp = new TempDirectory();
        var path = temp.File("h.scene.json");
        var document = new EditorSceneDocument(
            "Harbor",
            new[] { new SceneNode(new SceneNodeId(1), "tank", "models/tank.glb", EditorTransform.Identity) });

        EditorSceneFileStore.Save(path, document).IsOk.Should().BeTrue();
        var loaded = EditorSceneFileStore.Load(path);

        loaded.IsOk.Should().BeTrue();
        loaded.Unwrap().Should().BeEquivalentTo(document);
    }

    [Fact]
    public void Save_is_atomic_overwrite()
    {
        using var temp = new TempDirectory();
        var path = temp.File("h.scene.json");

        EditorSceneFileStore.Save(path, EditorSceneDocument.Empty("first")).IsOk.Should().BeTrue();
        EditorSceneFileStore.Save(path, EditorSceneDocument.Empty("second")).IsOk.Should().BeTrue();

        EditorSceneFileStore.Load(path).Unwrap().Name.Should().Be("second");
    }

    [Fact]
    public void Loading_a_missing_file_is_io_failed()
    {
        using var temp = new TempDirectory();

        var result = EditorSceneFileStore.Load(temp.File("missing.json"));

        result.IsErr.Should().BeTrue();
        result.UnwrapErr().Code.Should().Be(ErrorCode.SaveIoFailed);
    }

    [Fact]
    public void Loading_malformed_json_is_settings_corrupt()
    {
        using var temp = new TempDirectory();
        var path = temp.File("bad.json");
        File.WriteAllText(path, "{ not json");

        var result = EditorSceneFileStore.Load(path);

        result.IsErr.Should().BeTrue();
        result.UnwrapErr().Code.Should().Be(ErrorCode.SettingsCorrupt);
    }
}
