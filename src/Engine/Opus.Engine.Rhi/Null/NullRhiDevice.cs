using System.Collections.Generic;

namespace Opus.Engine.Rhi.Null;

/// <summary>
/// Headless RHI device. Returns no-op handles for every factory call, performs no GPU
/// work, never blocks on the device. Lets the entire engine boot in environments without
/// a GPU: unit tests, the asset-bake CLI, the future headless server.
///
/// All "resources" returned are inert — they carry their description fields for
/// introspection but do not allocate native memory. Disposal is a no-op (refcount → 0).
/// </summary>
public sealed class NullRhiDevice : IRhiDevice
{
    public RhiBackendKind Backend => RhiBackendKind.Null;

    public RhiCapabilities Capabilities => RhiCapabilities.None;

    public string AdapterName => "Null (headless)";

    public IRhiCommandList CreateCommandList(string debugName) => new NullCommandList(debugName);

    public IRhiBuffer CreateBuffer(RhiBufferDescription description) =>
        new NullBuffer(description.DebugName, description.SizeBytes, description.Usage);

    public IRhiTexture CreateTexture(RhiTextureDescription description) =>
        new NullTexture(description);

    public IRhiShader CreateShader(RhiShaderDescription description) =>
        new NullShader(description.DebugName, description.Stage);

    public IRhiPipeline CreatePipeline(RhiPipelineDescription description) =>
        new NullPipeline(description.DebugName, description.IsGraphics);

    public void WaitForIdle()
    {
        // No GPU work outstanding — return immediately.
    }

    public void Dispose()
    {
    }
}

public sealed class NullCommandList : IRhiCommandList
{
    public NullCommandList(string debugName)
    {
        DebugName = debugName;
    }

    public string DebugName { get; }

    public bool IsOpen { get; private set; }

    public void Begin(uint frameSlot) => IsOpen = true;

    public void End() => IsOpen = false;

    public void Dispose()
    {
    }
}

public sealed class NullBuffer : IRhiBuffer
{
    public NullBuffer(string debugName, int sizeBytes, RhiBufferUsage usage)
    {
        DebugName = debugName;
        SizeBytes = sizeBytes;
        Usage = usage;
    }

    public string DebugName { get; }

    public int SizeBytes { get; }

    public RhiBufferUsage Usage { get; }

    public void Dispose()
    {
    }
}

public sealed class NullTexture : IRhiTexture
{
    public NullTexture(RhiTextureDescription description)
    {
        DebugName = description.DebugName;
        Width = description.Width;
        Height = description.Height;
        MipLevels = description.MipLevels;
        Format = description.Format;
        Usage = description.Usage;
    }

    public string DebugName { get; }

    public int Width { get; }

    public int Height { get; }

    public int MipLevels { get; }

    public RhiTextureFormat Format { get; }

    public RhiTextureUsage Usage { get; }

    public void Dispose()
    {
    }
}

public sealed class NullShader : IRhiShader
{
    public NullShader(string debugName, RhiShaderStage stage)
    {
        DebugName = debugName;
        Stage = stage;
    }

    public string DebugName { get; }

    public RhiShaderStage Stage { get; }

    public void Dispose()
    {
    }
}

public sealed class NullPipeline : IRhiPipeline
{
    public NullPipeline(string debugName, bool isGraphics)
    {
        DebugName = debugName;
        IsGraphics = isGraphics;
    }

    public string DebugName { get; }

    public bool IsGraphics { get; }

    public void Dispose()
    {
    }
}
