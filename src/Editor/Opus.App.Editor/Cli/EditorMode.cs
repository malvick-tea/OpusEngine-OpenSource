namespace Opus.App.Editor.Cli;

/// <summary>
/// Top-level dispatch modes for the Opus editor CLI. The headless modes let the scene document, command
/// core, and pseudo-code mirror be driven and inspected without a GPU; <see cref="Window"/> opens the live
/// D3D12 authoring viewport over the same document cores.
/// </summary>
public enum EditorMode
{
    /// <summary>Print the usage banner and exit.</summary>
    Help,

    /// <summary>Create a new empty scene document file at the given path.</summary>
    New,

    /// <summary>Load a scene document and print its summary plus the pseudo-code mirror.</summary>
    Show,

    /// <summary>Load a scene document and print only its pseudo-code (DSL) mirror.</summary>
    Dsl,

    /// <summary>Inspect an imported glTF/GLB model and print a summary.</summary>
    Inspect,

    /// <summary>Place a model into a scene file and print the updated pseudo-code.</summary>
    Place,

    /// <summary>Remove a node from a scene by id and print the updated pseudo-code.</summary>
    SceneRemove,

    /// <summary>Rename a scene node by id and print the updated pseudo-code.</summary>
    SceneRename,

    /// <summary>Move a scene node by id to a new position and print the updated pseudo-code.</summary>
    SceneMove,

    /// <summary>Set a scene node's rotation (Euler degrees) by id and print the updated pseudo-code.</summary>
    SceneRotate,

    /// <summary>Set a scene node's scale by id and print the updated pseudo-code.</summary>
    SceneScale,

    /// <summary>Duplicate a scene node by id and print the updated pseudo-code.</summary>
    SceneDuplicate,

    /// <summary>Parent a scene node (by id) under another node and print the updated pseudo-code.</summary>
    SceneParent,

    /// <summary>Detach a scene node (by id) to a root and print the updated pseudo-code.</summary>
    SceneUnparent,

    /// <summary>Add a light (directional / point / spot) to a scene and print the updated pseudo-code.</summary>
    LightAdd,

    /// <summary>Remove a light from a scene by id and print the updated pseudo-code.</summary>
    LightRemove,

    /// <summary>Retune an existing light by id (colour / intensity / position / direction / range / cone).</summary>
    LightEdit,

    /// <summary>Report a scene's content cost (assets, instances, estimated geometry).</summary>
    Report,

    /// <summary>Validate PBR material sets on disk against the authoring convention.</summary>
    Materials,

    /// <summary>Create a new empty animation state-graph document file.</summary>
    AnimNew,

    /// <summary>Load an animation graph and print its summary, validation, and pseudo-code.</summary>
    AnimShow,

    /// <summary>Add a state to an animation graph and print the updated pseudo-code.</summary>
    AnimState,

    /// <summary>Wire a transition between two states and print the updated pseudo-code.</summary>
    AnimTransition,

    /// <summary>Remove a state (cascading its transitions) and print the updated pseudo-code.</summary>
    AnimRemoveState,

    /// <summary>Remove a transition between two states and print the updated pseudo-code.</summary>
    AnimRemoveTransition,

    /// <summary>Create a new empty editor-project manifest file.</summary>
    ProjectNew,

    /// <summary>Load a project manifest and print its summary and pseudo-code.</summary>
    ProjectShow,

    /// <summary>Add a reference (scene / graph / content root / material root) to a project.</summary>
    ProjectAdd,

    /// <summary>Check that every reference in a project resolves on disk.</summary>
    ProjectCheck,

    /// <summary>Open and validate every referenced scene, graph, and material set in a project.</summary>
    ProjectDoctor,

    /// <summary>Open the live D3D12 authoring window over a scene document.</summary>
    Window,
}
