using System.Linq;
using FluentAssertions;
using Opus.Engine.Rhi.Direct3D12;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Direct3D12.Tests.Fixtures;

internal static class D3D12DebugAssertions
{
    public static void Clear(D3D12RhiDevice device) =>
        device.DebugMessenger?.Clear();

    public static void ShouldHaveNoErrors(D3D12RhiDevice device)
    {
        var messenger = device.DebugMessenger;
        if (messenger is null)
        {
            return;
        }

        var errors = messenger.Snapshot()
            .Where(m => m.Severity is MessageSeverity.Error or MessageSeverity.Corruption)
            .Select(m => m.ToString())
            .ToArray();
        errors.Should().BeEmpty("the D3D12 debug layer should not report errors during the smoke path");
    }
}
