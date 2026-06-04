using System.Collections.Generic;
using System.Numerics;
using FluentAssertions;
using Opus.Engine.Ui;
using Opus.Engine.Ui.Direct3D12.Batching;
using Opus.Engine.Ui.Direct3D12.Text;
using Xunit;

namespace Opus.Engine.Ui.Direct3D12.Tests.Text;

public sealed class UiTextLayoutTests
{
    [Fact]
    public void Measure_scales_advances_for_latin_space_and_cyrillic()
    {
        var atlas = CreateAtlas();

        var width = UiTextLayout.Measure("A Д", fontSize: 40, atlas);

        width.Should().Be(46);
    }

    [Fact]
    public void Append_emits_only_rasterized_glyph_quads()
    {
        var atlas = CreateAtlas();
        var batch = new UiQuadBatch(initialQuadCapacity: 1);

        var width = UiTextLayout.Append(batch, "A Д", 10, 20, 40, Color.White, atlas);

        width.Should().Be(46f);
        batch.QuadCount.Should().Be(2);
        var vertices = batch.Vertices.ToArray();
        vertices[0].Position.Should().Be(new Vector2(12, 30));
        vertices[6].Position.Should().Be(new Vector2(36, 28));
    }

    private static BakedGlyphAtlas CreateAtlas()
    {
        var glyphs = new Dictionary<int, GlyphMetric>
        {
            ['A'] = new(new GlyphUvBox(0.1f, 0.1f, 0.2f, 0.2f), 8, 10, 1f, -10f, 9f),
            [' '] = new(new GlyphUvBox(0f, 0f, 0f, 0f), 0, 0, 0f, 0f, 4f),
            ['Д'] = new(new GlyphUvBox(0.3f, 0.1f, 0.4f, 0.25f), 9, 11, 0f, -11f, 10f),
        };

        return new BakedGlyphAtlas(
            new byte[16],
            width: 4,
            height: 4,
            whiteUv: new Vector2(0.5f, 0.5f),
            glyphs,
            bakePixelHeight: 20f,
            ascent: 15f,
            lineHeight: 24f);
    }
}
