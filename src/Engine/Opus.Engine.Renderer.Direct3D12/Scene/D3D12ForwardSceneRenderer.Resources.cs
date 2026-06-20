using System;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;
using Silk.NET.DXGI;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

public sealed unsafe partial class D3D12ForwardSceneRenderer
{
    private static RendererResources CreateResources(
        D3D12RhiDevice device,
        D3D12ShaderCompiler compiler,
        Format backbufferFormat,
        int viewportWidth,
        int viewportHeight,
        string namePrefix)
    {
        D3D12RootSignature? sceneRootSignature = null;
        D3D12GraphicsPipeline? scenePipeline = null;
        D3D12RootSignature? tonemapRootSignature = null;
        D3D12GraphicsPipeline? tonemapPipeline = null;
        ForwardSceneTargets? targets = null;
        D3D12Buffer[]? sceneConstantBuffers = null;
        SceneInstanceBufferRing? instanceRing = null;

        try
        {
            var sceneVs = compiler.Compile(
                ForwardSceneShaders.SceneVertexShader,
                "main",
                "vs_6_0",
                $"{namePrefix}.scene.vs.hlsl");
            var scenePs = compiler.Compile(
                ForwardSceneShaders.ScenePixelShader,
                "main",
                "ps_6_0",
                $"{namePrefix}.scene.ps.hlsl");
            sceneRootSignature = D3D12RootSignatureFactory.CreateInstancedPbrScene(
                device,
                InstancedDrawConstants.Num32BitValues);
            scenePipeline = D3D12GraphicsPipelineFactory.CreatePosNormalUvLitDepth(
                device,
                sceneRootSignature,
                sceneVs,
                scenePs,
                renderTargetFormat: Format.FormatR16G16B16A16Float,
                depthStencilFormat: Format.FormatD32Float);

            var tonemapVs = compiler.Compile(
                TonemapShaders.VertexShader,
                "main",
                "vs_6_0",
                $"{namePrefix}.tonemap.vs.hlsl");
            var tonemapPs = compiler.Compile(
                TonemapShaders.PixelShader,
                "main",
                "ps_6_0",
                $"{namePrefix}.tonemap.ps.hlsl");
            tonemapRootSignature = D3D12RootSignatureFactory.CreateTonemapPost(device);
            tonemapPipeline = D3D12GraphicsPipelineFactory.CreateFullscreenPostProcess(
                device,
                tonemapRootSignature,
                tonemapVs,
                tonemapPs,
                backbufferFormat);

            targets = new ForwardSceneTargets(device, viewportWidth, viewportHeight, namePrefix);
            sceneConstantBuffers = CreateSceneConstantBuffers(device, namePrefix);
            instanceRing = new SceneInstanceBufferRing(
                device,
                D3D12SwapChain.BufferCount,
                namePrefix);

            return new RendererResources(
                sceneRootSignature,
                scenePipeline,
                tonemapRootSignature,
                tonemapPipeline,
                targets,
                sceneConstantBuffers,
                instanceRing);
        }
        catch
        {
            instanceRing?.Dispose();
            DisposeBuffers(sceneConstantBuffers);
            targets?.Dispose();
            tonemapPipeline?.Dispose();
            tonemapRootSignature?.Dispose();
            scenePipeline?.Dispose();
            sceneRootSignature?.Dispose();
            throw;
        }
    }

    private static D3D12Buffer[] CreateSceneConstantBuffers(
        D3D12RhiDevice device,
        string namePrefix)
    {
        var buffers = new D3D12Buffer[D3D12SwapChain.BufferCount];
        try
        {
            for (var index = 0; index < buffers.Length; index++)
            {
                buffers[index] = device.CreateGraphicsBuffer(new RhiBufferDescription(
                    $"{namePrefix}.scene.cb.{index}",
                    SceneCbSize,
                    RhiBufferUsage.Uniform));
            }

            return buffers;
        }
        catch
        {
            DisposeBuffers(buffers);
            throw;
        }
    }

    private static void DisposeBuffers(D3D12Buffer[]? buffers)
    {
        if (buffers is null)
        {
            return;
        }

        foreach (var buffer in buffers)
        {
            buffer?.Dispose();
        }
    }

    private sealed record RendererResources(
        D3D12RootSignature SceneRootSignature,
        D3D12GraphicsPipeline ScenePipeline,
        D3D12RootSignature TonemapRootSignature,
        D3D12GraphicsPipeline TonemapPipeline,
        ForwardSceneTargets Targets,
        D3D12Buffer[] SceneConstantBuffers,
        SceneInstanceBufferRing InstanceRing);
}
