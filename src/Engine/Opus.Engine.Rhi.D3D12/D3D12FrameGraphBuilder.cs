using System.Collections.Generic;
using Opus.Engine.FrameGraph;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>
/// Builder handed to <see cref="D3D12RenderPass.Setup"/> for declaring resource usages.
/// R-5.a stores the declarations but doesn't act on them yet — they exist so R-5.b can
/// scan the pass list and insert barrier transitions automatically.
/// </summary>
public sealed class D3D12FrameGraphBuilder
{
    private readonly List<ResourceUsageDeclaration> _usages;

    internal D3D12FrameGraphBuilder(List<ResourceUsageDeclaration> usages)
    {
        _usages = usages;
    }

    public void Read(FrameGraphResource handle) =>
        _usages.Add(new ResourceUsageDeclaration(handle, FrameGraphResourceUsage.Read));

    public void Write(FrameGraphResource handle) =>
        _usages.Add(new ResourceUsageDeclaration(handle, FrameGraphResourceUsage.Write));

    public void ColorTarget(FrameGraphResource handle) =>
        _usages.Add(new ResourceUsageDeclaration(handle, FrameGraphResourceUsage.ColorTarget));

    public void DepthTarget(FrameGraphResource handle) =>
        _usages.Add(new ResourceUsageDeclaration(handle, FrameGraphResourceUsage.DepthTarget));

    /// <summary>Pass writes to this resource through a UAV — typically a compute pass
    /// declaring its output texture. Graph maps this to <c>ResourceStates.UnorderedAccess</c>.</summary>
    public void UnorderedAccess(FrameGraphResource handle) =>
        _usages.Add(new ResourceUsageDeclaration(handle, FrameGraphResourceUsage.UnorderedAccess));
}
