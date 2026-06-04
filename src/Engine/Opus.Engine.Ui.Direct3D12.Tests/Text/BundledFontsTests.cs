using FluentAssertions;
using Opus.Engine.Ui.Direct3D12.Text;
using Xunit;

namespace Opus.Engine.Ui.Direct3D12.Tests.Text;

/// <summary>Headless checks for the Latin font source used by the D3D12 text atlas.</summary>
public sealed class BundledFontsTests
{
    [Fact]
    public void Latin_face_opens_from_embedded_resource_or_system_fallback()
    {
        using var face = OpenLatinFace();

        face.Should().NotBeNull("the source-only tree relies on host font fallback when no font asset is embedded");
    }

    [Theory]
    [InlineData('A')]
    [InlineData('z')]
    [InlineData('0')]
    [InlineData(',')]
    [InlineData('\u0414')]
    [InlineData('\u044f')]
    [InlineData('\u0416')]
    public void Latin_face_carries_the_glyph(int codepoint)
    {
        using var face = OpenLatinFace();

        face!.HasGlyph(codepoint).Should().BeTrue();
    }

    [Fact]
    public void Latin_face_rasterizes_a_cyrillic_glyph_to_real_coverage()
    {
        using var face = OpenLatinFace();

        var raster = face!.Rasterize('\u0414', pixelHeight: 32f);

        raster.HasRaster.Should().BeTrue("a Cyrillic capital De must produce real coverage pixels");
        raster.Advance.Should().BeGreaterThan(0f);
    }

    private static StbGlyphSource? OpenLatinFace() =>
        BundledFonts.TryOpenLatinFace() ?? SystemFontLoader.LoadFirstAvailable(FontFaceCandidates.Latin);
}
