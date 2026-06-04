namespace Opus.Engine.Rhi;

/// <summary>
/// Identifies which native GPU API the active <see cref="IRhiDevice"/> dispatches to.
/// Surface-area marker so a caller can ask the device "are you Vulkan?" without
/// downcasting to a backend-specific type. Used for capability checks and debug logging.
/// </summary>
public enum RhiBackendKind : byte
{
    /// <summary>Headless backend that executes no GPU work — for tests, asset bake,
    /// and server boot. Always available.</summary>
    Null = 0,

    /// <summary>Microsoft Direct3D 12 on Windows. Primary backend per ADR-0016.</summary>
    D3D12 = 1,

    /// <summary>Khronos Vulkan. Cross-platform target (Linux + Windows). Implementation
    /// deferred to a future phase per ADR-0016.</summary>
    Vulkan = 2,

    /// <summary>Apple Metal. macOS / iOS target. Implementation deferred.</summary>
    Metal = 3,
}
