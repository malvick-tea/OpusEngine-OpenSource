using System.Collections.Generic;
using System.Numerics;
using FluentAssertions;
using Opus.Engine.FrameGraph;
using Opus.Engine.Renderer;
using Opus.Engine.Renderer.Null;
using Opus.Engine.Rhi;
using Xunit;

namespace Opus.Engine.Renderer.Tests;

public sealed class NullRendererTests
{
    [Fact]
    public void Null_renderer_exposes_null_backend_device()
    {
        using var renderer = new NullRenderer();

        renderer.Device.Backend.Should().Be(RhiBackendKind.Null);
        renderer.Device.Capabilities.Should().Be(RhiCapabilities.None);
        renderer.Device.AdapterName.Should().Be("Null (headless)");
    }

    [Fact]
    public void Frame_index_advances_monotonically_per_begin()
    {
        using var renderer = new NullRenderer();

        var c1 = renderer.BeginFrame(SampleCameras(), SampleLighting(), SamplePostFx());
        var f1 = c1.FrameIndex;
        renderer.EndFrame(c1);

        var c2 = renderer.BeginFrame(SampleCameras(), SampleLighting(), SamplePostFx());
        var f2 = c2.FrameIndex;
        renderer.EndFrame(c2);

        f2.Should().BeGreaterThan(f1);
    }

    [Fact]
    public void Frame_context_carries_captured_cameras_lighting_postfx()
    {
        using var renderer = new NullRenderer();

        var cameras = SampleCameras();
        var lighting = SampleLighting();
        var post = SamplePostFx();

        var context = renderer.BeginFrame(cameras, lighting, post);

        context.Cameras.Should().Be(cameras);
        context.Lighting.Should().Be(lighting);
        context.PostFx.Should().Be(post);

        renderer.EndFrame(context);
    }

    [Fact]
    public void Frame_graph_compiles_and_executes_passes_added_during_frame()
    {
        using var renderer = new NullRenderer();
        var pass = new RecordingPass();

        var context = renderer.BeginFrame(SampleCameras(), SampleLighting(), SamplePostFx());
        renderer.FrameGraph.AddPass(pass);
        renderer.EndFrame(context);

        pass.SetupCalled.Should().BeTrue();
        pass.ExecuteCalled.Should().BeTrue();
    }

    [Fact]
    public void Renderable_records_carry_pipeline_layer_and_transform()
    {
        var renderable = new Renderable(
            MeshHandle: 1,
            MaterialHandle: 2,
            Pipeline: MaterialPipeline.Toon,
            WorldTransform: Matrix4x4.CreateTranslation(1f, 2f, 3f),
            Layers: RenderableLayerMask.Opaque | RenderableLayerMask.ShadowCaster);

        renderable.Pipeline.Should().Be(MaterialPipeline.Toon);
        (renderable.Layers & RenderableLayerMask.ShadowCaster).Should().Be(RenderableLayerMask.ShadowCaster);
    }

    [Fact]
    public void Camera_set_supports_single_main()
    {
        var main = new CameraSetup(
            Matrix4x4.Identity, Matrix4x4.Identity,
            Vector3.Zero, Vector3.UnitZ,
            0.1f, 1000f, 1.2f, 16f / 9f);

        var set = FrameCameraSet.SingleMain(main);

        set.Main.Should().Be(main);
        set.Auxiliary.Should().BeEmpty();
    }

    [Fact]
    public void Null_frame_graph_rejects_execute_before_compile()
    {
        using var renderer = new NullRenderer();
        var context = renderer.BeginFrame(SampleCameras(), SampleLighting(), SamplePostFx());

        // EndFrame compiles + executes — calling Execute directly without prior Compile
        // is the failure mode we guard against.
        var act = () => renderer.FrameGraph.Execute();
        act.Should().Throw<System.InvalidOperationException>().WithMessage("*before Compile*");

        renderer.EndFrame(context);
    }

    private static FrameCameraSet SampleCameras() => FrameCameraSet.SingleMain(
        new CameraSetup(
            Matrix4x4.Identity, Matrix4x4.Identity,
            Vector3.Zero, Vector3.UnitZ,
            0.1f, 1000f, 1.2f, 16f / 9f));

    private static LightingSetup SampleLighting() => new(
        new DirectionalLight(Vector3.UnitY * -1, Vector3.One, 1f, true),
        new List<LocalLight>(),
        new SkySetup(Vector3.UnitY * -1, Vector3.One, Vector3.UnitX, 0f, 0));

    private static PostFxSetup SamplePostFx() => new(
        TonemapOperator.AcesFilmic,
        new BloomSetup(true, 1.0f, 0.05f, 5),
        new ColourGradingSetup(false, 0, 1f, 1f),
        AntiAliasingMode.Taa,
        UpscaleMode.None,
        0f);

    private sealed class RecordingPass : IRenderPass
    {
        public string Name => "RecordingPass";

        public bool SetupCalled { get; private set; }

        public bool ExecuteCalled { get; private set; }

        public void Setup(IFrameGraphBuilder builder) => SetupCalled = true;

        public void Execute(IRenderPassContext context) => ExecuteCalled = true;
    }
}
