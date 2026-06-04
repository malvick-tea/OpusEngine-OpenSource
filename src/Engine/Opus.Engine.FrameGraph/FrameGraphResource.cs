using Opus.Engine.Rhi;

namespace Opus.Engine.FrameGraph;

/// <summary>
/// Opaque handle to a resource declared inside a frame graph build. The graph compiler
/// resolves the handle to a concrete <see cref="IRhiTexture"/> or <see cref="IRhiBuffer"/>
/// at execute time — possibly aliased onto another transient resource that doesn't
/// overlap in time.
///
/// Handles are stable for the lifetime of one compiled graph. Across recompiles, prefer
/// to retrieve by name rather than caching handles.
/// </summary>
public readonly record struct FrameGraphResource(int Id, FrameGraphResourceKind Kind);

public enum FrameGraphResourceKind : byte
{
    Buffer = 0,
    Texture = 1,
}

/// <summary>How a pass interacts with a resource. The graph uses these to:
/// (a) insert correct barriers between passes,
/// (b) extend resource lifetime through the dependency chain,
/// (c) decide transient aliasing — same-format resources whose lifetimes don't overlap
///     can share VRAM.</summary>
[System.Flags]
public enum FrameGraphResourceUsage : ushort
{
    None = 0,
    Read = 1 << 0,
    Write = 1 << 1,
    ReadWrite = Read | Write,

    /// <summary>Used as a colour render target (texture only).</summary>
    ColorTarget = 1 << 2,

    /// <summary>Used as a depth/stencil render target (texture only).</summary>
    DepthTarget = 1 << 3,

    /// <summary>Used in compute as a UAV.</summary>
    UnorderedAccess = 1 << 4,

    /// <summary>Used in indirect-args slot.</summary>
    IndirectArgs = 1 << 5,
}

public readonly record struct FrameGraphTextureDescription(
    string DebugName,
    int Width,
    int Height,
    int MipLevels,
    RhiTextureFormat Format);

public readonly record struct FrameGraphBufferDescription(
    string DebugName,
    int SizeBytes,
    RhiBufferUsage Usage);
