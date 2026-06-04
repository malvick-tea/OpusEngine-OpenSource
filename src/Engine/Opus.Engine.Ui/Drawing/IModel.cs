using System;
using System.Numerics;

namespace Opus.Engine.Ui;

/// <summary>
/// Opaque handle to a renderable 3D mesh + materials. Loaders (one per file format)
/// produce these; <see cref="IModelRenderer"/> consumes them. Concrete types live in the
/// backend assembly (e.g. <c>Engine.Ui.Raylib.RaylibModel</c>) and are never exposed
/// to gameplay/UI code.
///
/// Lifetime: hosts dispose at shutdown. Models cached by <see cref="IModelLoader"/>
/// are owned by the loader; if you load the same path twice you get the same instance.
/// </summary>
public interface IModel : IDisposable
{
    /// <summary>True after the loader successfully read the file. False handles render as no-ops.</summary>
    bool IsValid { get; }

    Vector3 BoundsMin { get; }

    Vector3 BoundsMax { get; }
}

/// <summary>Loads + caches 3D models from VFS-resolved paths.</summary>
public interface IModelLoader
{
    /// <summary>
    /// Returns the model at the given <c>res://</c> or <c>user://</c> path. Subsequent
    /// calls with the same path return the cached instance. On failure returns a model
    /// whose <see cref="IModel.IsValid"/> is false (renders no-op so callers don't crash).
    /// </summary>
    IModel Load(string virtualPath);
}

/// <summary>
/// 3D draw context. Wraps Raylib's <c>BeginMode3D</c>/<c>EndMode3D</c> bracket; nothing
/// outside the bracket may call <see cref="DrawModel"/>. Exists alongside (not inside)
/// <see cref="IDrawSurface"/> so 2D and 3D layers stay distinct — proper render-graph
/// semantics arrive with Engine.Renderer in M3.
/// </summary>
public interface IModelRenderer
{
    void BeginScene(in CameraView3D camera);

    void DrawModel(IModel model, Vector3 position, float scale, Color tint);

    void DrawModelEx(
        IModel model,
        Vector3 position,
        Vector3 rotationAxis,
        float rotationDegrees,
        Vector3 scale,
        Color tint);

    void EndScene();
}
