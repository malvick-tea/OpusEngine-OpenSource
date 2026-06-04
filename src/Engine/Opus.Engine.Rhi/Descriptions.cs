namespace Opus.Engine.Rhi;

/// <summary>Allowed read / write usage of a buffer at the API level. A buffer can carry
/// multiple usages (vertex + indirect-args, for example) via the bitfield.</summary>
[System.Flags]
public enum RhiBufferUsage : ushort
{
    None = 0,
    Vertex = 1 << 0,
    Index = 1 << 1,
    Uniform = 1 << 2,
    Structured = 1 << 3,
    IndirectArgs = 1 << 4,
    Staging = 1 << 5,
    Raytracing = 1 << 6,

    /// <summary>Buffer is written by a compute shader via a UAV. Forces a default-heap
    /// allocation (instead of the upload-heap CPU-visible default) so the GPU can write
    /// at full bandwidth and the resource starts in <c>D3D12_RESOURCE_STATE_UNORDERED_ACCESS</c>.</summary>
    UnorderedAccess = 1 << 7,

    /// <summary>CPU-readable transfer destination. Used by screenshot/readback paths that
    /// copy GPU textures into a row-pitched buffer after rendering.</summary>
    Readback = 1 << 8,
}

/// <summary>Allowed read / write usage of a texture at the API level.</summary>
[System.Flags]
public enum RhiTextureUsage : ushort
{
    None = 0,
    Sampled = 1 << 0,
    ColorTarget = 1 << 1,
    DepthStencilTarget = 1 << 2,
    UnorderedAccess = 1 << 3,
    Staging = 1 << 4,
}

/// <summary>Pixel format. Subset of the full DXGI / Vulkan format set — runtime
/// renderer adds more as passes need them. Phase R-0 ships the common 4: BGRA8_UNORM
/// (swap chain), R16G16B16A16_FLOAT (HDR scene), D32_SFLOAT (depth), R8_UNORM
/// (single-channel masks). The block-compressed trio (BC7 sRGB / BC7 linear / BC5) backs
/// the disk-loaded PBR material sets: base colour + emissive sample as BC7 sRGB, packed ORM
/// as BC7 linear, tangent-space normals as BC5 — a 4:1 VRAM win over uploading 4K maps as
/// raw RGBA8.</summary>
public enum RhiTextureFormat : ushort
{
    Unknown = 0,
    Bgra8UnormSrgb = 1,
    Rgba8Unorm = 2,
    Rgba16Float = 3,
    R8Unorm = 4,
    D32Float = 5,
    D24UnormS8Uint = 6,
    Rg16Float = 7,
    R32Float = 8,
    Bc7UnormSrgb = 9,
    Bc7Unorm = 10,
    Bc5Unorm = 11,
}

public enum RhiShaderStage : byte
{
    Vertex = 0,
    Pixel = 1,
    Compute = 2,
    Mesh = 3,
    Amplification = 4,
    Hull = 5,
    Domain = 6,
    Geometry = 7,
    Raygen = 16,
    Miss = 17,
    ClosestHit = 18,
    AnyHit = 19,
    Intersection = 20,
    Callable = 21,
}

public readonly record struct RhiBufferDescription(
    string DebugName,
    int SizeBytes,
    RhiBufferUsage Usage);

public readonly record struct RhiTextureDescription(
    string DebugName,
    int Width,
    int Height,
    int MipLevels,
    RhiTextureFormat Format,
    RhiTextureUsage Usage);

public readonly record struct RhiShaderDescription(
    string DebugName,
    RhiShaderStage Stage,
    System.ReadOnlyMemory<byte> Bytecode);

public readonly record struct RhiPipelineDescription(
    string DebugName,
    bool IsGraphics,
    System.Collections.Generic.IReadOnlyList<IRhiShader> Shaders);
