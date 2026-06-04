using FluentAssertions;
using Opus.Engine.Direct3D12.Tests.Fixtures;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;
using Silk.NET.Direct3D12;
using Xunit;

namespace Opus.Engine.Direct3D12.Tests.Rhi;

public sealed unsafe class D3D12RhiDeviceSmokeTests
{
    [SkippableFact]
    public void Device_reports_adapter_and_creates_basic_resources()
    {
        using var device = D3D12SmokeHost.OpenDevice();

        device.Backend.Should().Be(RhiBackendKind.D3D12);
        device.AdapterName.Should().NotBeNullOrWhiteSpace();

        using var buffer = device.CreateGraphicsBuffer(new RhiBufferDescription(
            "smoke.vertex-buffer",
            SizeBytes: 256,
            RhiBufferUsage.Vertex));
        buffer.SizeBytes.Should().Be(256);
        buffer.GpuVirtualAddress.Should().NotBe(0);

        using var texture = device.CreateGraphicsTexture(new RhiTextureDescription(
            "smoke.render-target",
            Width: 32,
            Height: 32,
            MipLevels: 1,
            RhiTextureFormat.Rgba8Unorm,
            RhiTextureUsage.ColorTarget));
        texture.Width.Should().Be(32);
        texture.Height.Should().Be(32);
    }

    [SkippableFact]
    public void Command_list_clears_render_target_and_drains_gpu()
    {
        using var device = D3D12SmokeHost.OpenDevice();
        using var texture = device.CreateGraphicsTexture(new RhiTextureDescription(
            "smoke.clear-target",
            Width: 32,
            Height: 32,
            MipLevels: 1,
            RhiTextureFormat.Rgba8Unorm,
            RhiTextureUsage.ColorTarget));
        using var commandList = device.CreateGraphicsCommandList("smoke.clear", frameSlots: 1);
        ID3D12DescriptorHeap* rtvHeap = device.CreateRtvDescriptorHeap(1u);

        try
        {
            var rtv = device.CreateRenderTargetView(texture, rtvHeap);
            D3D12DebugAssertions.Clear(device);

            commandList.Begin(0u);
            commandList.OMSetRenderTarget(rtv);
            commandList.ClearRenderTargetView(rtv, 0.1f, 0.2f, 0.3f, 1f);
            commandList.End();
            commandList.ExecuteOn(device);
            device.WaitForIdle();

            D3D12DebugAssertions.ShouldHaveNoErrors(device);
        }
        finally
        {
            rtvHeap->Release();
        }
    }

    [SkippableFact]
    public void Command_list_clears_render_target_and_reads_back_screenshot()
    {
        using var device = D3D12SmokeHost.OpenDevice();
        using var texture = device.CreateGraphicsTexture(new RhiTextureDescription(
            "smoke.readback-target",
            Width: 16,
            Height: 16,
            MipLevels: 1,
            RhiTextureFormat.Rgba8Unorm,
            RhiTextureUsage.ColorTarget));
        using var readback = D3D12TextureReadback.Create(device, texture, "smoke.readback-target.capture");
        using var commandList = device.CreateGraphicsCommandList("smoke.readback", frameSlots: 1);
        ID3D12DescriptorHeap* rtvHeap = device.CreateRtvDescriptorHeap(1u);

        try
        {
            var rtv = device.CreateRenderTargetView(texture, rtvHeap);
            D3D12DebugAssertions.Clear(device);

            commandList.Begin(0u);
            commandList.OMSetRenderTarget(rtv);
            commandList.ClearRenderTargetView(rtv, 0.25f, 0.5f, 0.75f, 1f);
            readback.RecordCopyFrom(commandList, texture, ResourceStates.RenderTarget, ResourceStates.RenderTarget);
            commandList.End();
            commandList.ExecuteOn(device);
            device.WaitForIdle();

            var screenshot = readback.ReadRgba8();
            screenshot.Width.Should().Be(16);
            screenshot.Height.Should().Be(16);
            screenshot.Rgba8[0].Should().BeInRange((byte)55, (byte)75);
            screenshot.Rgba8[1].Should().BeInRange((byte)120, (byte)140);
            screenshot.Rgba8[2].Should().BeInRange((byte)180, (byte)200);
            screenshot.Rgba8[3].Should().Be(255);
            D3D12DebugAssertions.ShouldHaveNoErrors(device);
        }
        finally
        {
            rtvHeap->Release();
        }
    }
}
