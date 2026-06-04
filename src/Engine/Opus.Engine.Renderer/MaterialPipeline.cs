namespace Opus.Engine.Renderer;

/// <summary>
/// Discriminator between the two fragment-shader pipelines per ADR-0025. Carried on
/// every <see cref="IRenderable"/> so the renderer knows which shader to bind without
/// inspecting material metadata.
///
/// Same scene lighting feeds both pipelines — the difference is in the BRDF
/// interpretation:
/// <list type="bullet">
/// <item><description><see cref="Pbr"/> — continuous gradient, metallic-roughness BRDF, used for tanks,
///     terrain, weapons, environment.</description></item>
/// <item><description><see cref="Toon"/> — banded light response, rim light, inverted-hull outlines, used
///     for character meshes (skin / hair / eyes / clothing).</description></item>
/// </list>
/// </summary>
public enum MaterialPipeline : byte
{
    Pbr = 0,
    Toon = 1,
}
