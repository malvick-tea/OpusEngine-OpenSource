using System;
using System.Numerics;
using Opus.Engine.Ui;
using Opus.Engine.Ui.Direct3D12.Text;

namespace Opus.Engine.Ui.Direct3D12.Batching;

/// <summary>
/// Pure expansion of <see cref="IDrawSurface"/> primitives into <see cref="UiQuadBatch"/>
/// quads. Rectangles, lines and glyphs become textured quads (a solid fill points its UV
/// at the atlas white texel); circles and rings become a single quad shaded analytically
/// by the sprite pixel shader, so curvature stays crisp at any size.
/// </summary>
internal static class UiQuadGeometry
{
    /// <summary>An axis-aligned filled rectangle. <paramref name="whiteUv"/> is the atlas
    /// coordinate of the fully-opaque white texel, which turns the textured path into a
    /// flat colour fill.</summary>
    public static void Rect(UiQuadBatch batch, float x, float y, float w, float h, Color color, Vector2 whiteUv)
    {
        if (w <= 0f || h <= 0f)
        {
            return;
        }

        var rgba = UiQuadVertex.PackColor(color);
        AppendUniform(
            batch,
            new Vector2(x, y),
            new Vector2(x + w, y),
            new Vector2(x + w, y + h),
            new Vector2(x, y + h),
            whiteUv,
            rgba,
            UiDrawMode.Textured,
            Vector2.Zero);
    }

    /// <summary>A thick line from (<paramref name="x0"/>,<paramref name="y0"/>) to
    /// (<paramref name="x1"/>,<paramref name="y1"/>) as a rectangle swept along the
    /// segment normal. Zero-length segments emit nothing.</summary>
    public static void Line(UiQuadBatch batch, float x0, float y0, float x1, float y1, float thickness, Color color, Vector2 whiteUv)
    {
        var direction = new Vector2(x1 - x0, y1 - y0);
        var length = direction.Length();
        if (length < float.Epsilon || thickness <= 0f)
        {
            return;
        }

        var normal = new Vector2(-direction.Y, direction.X) / length;
        var half = normal * (thickness * 0.5f);
        var a = new Vector2(x0, y0);
        var b = new Vector2(x1, y1);
        AppendUniform(batch, a + half, b + half, b - half, a - half, whiteUv, UiQuadVertex.PackColor(color), UiDrawMode.Textured, Vector2.Zero);
    }

    /// <summary>One RGBA-sampled quad spanning the destination rectangle and covering the
    /// full 0..1 UV box of the bound texture. Used to composite externally-rendered targets
    /// (e.g. an offscreen scene viewport) into the UI batch — the bound SRV at flush time
    /// is what supplies the colour, not the glyph atlas.</summary>
    public static void TexturedRectRgba(UiQuadBatch batch, float x, float y, float w, float h, Color tint)
    {
        if (w <= 0f || h <= 0f)
        {
            return;
        }

        var rgba = UiQuadVertex.PackColor(tint);
        batch.AppendQuad(
            new UiQuadVertex(new Vector2(x, y), new Vector2(0f, 0f), rgba, UiDrawMode.TexturedRgba, Vector2.Zero),
            new UiQuadVertex(new Vector2(x + w, y), new Vector2(1f, 0f), rgba, UiDrawMode.TexturedRgba, Vector2.Zero),
            new UiQuadVertex(new Vector2(x + w, y + h), new Vector2(1f, 1f), rgba, UiDrawMode.TexturedRgba, Vector2.Zero),
            new UiQuadVertex(new Vector2(x, y + h), new Vector2(0f, 1f), rgba, UiDrawMode.TexturedRgba, Vector2.Zero));
    }

    /// <summary>One atlas-sampled glyph quad spanning the destination rectangle, mapped to
    /// the supplied atlas UV box.</summary>
    public static void Glyph(UiQuadBatch batch, float x, float y, float w, float h, GlyphUvBox uv, Color color)
    {
        if (w <= 0f || h <= 0f)
        {
            return;
        }

        var rgba = UiQuadVertex.PackColor(color);
        batch.AppendQuad(
            new UiQuadVertex(new Vector2(x, y), new Vector2(uv.U0, uv.V0), rgba, UiDrawMode.Textured, Vector2.Zero),
            new UiQuadVertex(new Vector2(x + w, y), new Vector2(uv.U1, uv.V0), rgba, UiDrawMode.Textured, Vector2.Zero),
            new UiQuadVertex(new Vector2(x + w, y + h), new Vector2(uv.U1, uv.V1), rgba, UiDrawMode.Textured, Vector2.Zero),
            new UiQuadVertex(new Vector2(x, y + h), new Vector2(uv.U0, uv.V1), rgba, UiDrawMode.Textured, Vector2.Zero));
    }

    /// <summary>A filled, analytically anti-aliased disc inscribed in its bounding quad.</summary>
    public static void Circle(UiQuadBatch batch, float centreX, float centreY, float radius, Color color)
    {
        if (radius <= 0f)
        {
            return;
        }

        AppendShapeQuad(batch, centreX, centreY, radius, color, UiDrawMode.FilledCircle, new Vector2(Feather(radius), 0f));
    }

    /// <summary>A stroked circle outline of the given pixel <paramref name="thickness"/>,
    /// shaded as an analytic annulus.</summary>
    public static void Ring(UiQuadBatch batch, float centreX, float centreY, float radius, float thickness, Color color)
    {
        if (radius <= 0f || thickness <= 0f)
        {
            return;
        }

        var innerFraction = Math.Clamp((radius - thickness) / radius, 0f, 1f);
        AppendShapeQuad(batch, centreX, centreY, radius, color, UiDrawMode.Ring, new Vector2(Feather(radius), innerFraction));
    }

    /// <summary>Edge feather in normalised-radius units — one pixel wide, so the analytic
    /// silhouette anti-aliases by exactly a pixel regardless of the disc's screen size.</summary>
    private static float Feather(float radius) => Math.Clamp(1f / radius, 0f, 1f);

    private static void AppendShapeQuad(UiQuadBatch batch, float centreX, float centreY, float radius, Color color, UiDrawMode mode, Vector2 shapeParams)
    {
        var rgba = UiQuadVertex.PackColor(color);
        var minX = centreX - radius;
        var minY = centreY - radius;
        var maxX = centreX + radius;
        var maxY = centreY + radius;
        batch.AppendQuad(
            new UiQuadVertex(new Vector2(minX, minY), new Vector2(0f, 0f), rgba, mode, shapeParams),
            new UiQuadVertex(new Vector2(maxX, minY), new Vector2(1f, 0f), rgba, mode, shapeParams),
            new UiQuadVertex(new Vector2(maxX, maxY), new Vector2(1f, 1f), rgba, mode, shapeParams),
            new UiQuadVertex(new Vector2(minX, maxY), new Vector2(0f, 1f), rgba, mode, shapeParams));
    }

    private static void AppendUniform(UiQuadBatch batch, Vector2 topLeft, Vector2 topRight, Vector2 bottomRight, Vector2 bottomLeft, Vector2 uv, uint rgba, UiDrawMode mode, Vector2 shapeParams)
    {
        batch.AppendQuad(
            new UiQuadVertex(topLeft, uv, rgba, mode, shapeParams),
            new UiQuadVertex(topRight, uv, rgba, mode, shapeParams),
            new UiQuadVertex(bottomRight, uv, rgba, mode, shapeParams),
            new UiQuadVertex(bottomLeft, uv, rgba, mode, shapeParams));
    }
}
