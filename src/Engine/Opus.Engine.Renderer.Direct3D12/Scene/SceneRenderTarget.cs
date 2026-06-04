using Opus.Engine.Rhi.Direct3D12;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Describes the colour target an offscreen <see cref="D3D12ForwardSceneRenderer"/>
/// pass writes into. Replaces the swap-chain back-buffer parameters the renderer pulls
/// from <see cref="D3D12Renderer.SwapChain"/> when rendering directly to the window —
/// the host populates this record from a <see cref="SceneViewportTarget"/> when the scene
/// composites into a UI quad instead of being presented.</summary>
/// <param name="Texture">The colour resource — used by the frame graph as the import
/// for the back-buffer slot.</param>
/// <param name="Rtv">The render-target view <see cref="TonemapPass"/> binds when it
/// writes the tonemapped LDR result.</param>
/// <param name="Width">Pixel width of the target — matches <see cref="Texture"/>.</param>
/// <param name="Height">Pixel height of the target.</param>
/// <param name="Format">The DXGI format <see cref="TonemapPass"/>'s PSO renders against.</param>
/// <param name="InitialState">Resource state on entry to the frame — the frame graph
/// imports the resource in this state and transitions away from it before tonemap writes.</param>
/// <param name="FinalState">Resource state required on exit from the frame — typically
/// <see cref="ResourceStates.PixelShaderResource"/> when the UI composes the target as a
/// sampled texture; <see cref="ResourceStates.Present"/> when the target is the swap chain.</param>
/// <param name="ClearAlpha">Alpha channel value the forward pass clears the HDR target
/// with. <c>0f</c> for UI-composite targets (sky pixels stay transparent so 2D chrome
/// reads through), <c>1f</c> for swap-chain back-buffer targets (opaque sky).</param>
public readonly record struct SceneRenderTarget(
    D3D12Texture Texture,
    CpuDescriptorHandle Rtv,
    int Width,
    int Height,
    Format Format,
    ResourceStates InitialState,
    ResourceStates FinalState,
    float ClearAlpha = 1f);
