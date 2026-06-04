using System;

namespace Opus.Engine.Rhi;

/// <summary>
/// Base of every GPU-backed resource (buffer, texture, shader, pipeline). Disposal
/// decrements the device-side refcount; the device defers actual native release until
/// the GPU's frame fence advances past the resource's last-use tick.
///
/// Existence of this base lets the frame graph track resource liveness uniformly without
/// caring whether it's a buffer or a texture.
/// </summary>
public interface IRhiResource : IDisposable
{
    string DebugName { get; }
}

/// <summary>Specialised GPU buffer — vertex, index, uniform, structured, indirect args, etc.</summary>
public interface IRhiBuffer : IRhiResource
{
    int SizeBytes { get; }

    RhiBufferUsage Usage { get; }
}

/// <summary>Specialised GPU texture — colour target, depth target, sampled SRV, UAV.</summary>
public interface IRhiTexture : IRhiResource
{
    int Width { get; }

    int Height { get; }

    int MipLevels { get; }

    RhiTextureFormat Format { get; }

    RhiTextureUsage Usage { get; }
}

/// <summary>Compiled shader binary, ready for pipeline construction. Holds DXIL on D3D12,
/// SPIR-V on Vulkan, MSL on Metal — backend chooses per <see cref="IRhiDevice.Backend"/>.</summary>
public interface IRhiShader : IRhiResource
{
    RhiShaderStage Stage { get; }
}

/// <summary>Compiled pipeline state object (PSO on D3D12, Pipeline on Vulkan).
/// Immutable bundle: shader stages + vertex layout + blend + rasteriser + depth /
/// stencil + render-target formats.</summary>
public interface IRhiPipeline : IRhiResource
{
    /// <summary>Whether this pipeline targets graphics (true) or compute (false).</summary>
    bool IsGraphics { get; }
}
