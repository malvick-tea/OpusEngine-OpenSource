using FluentAssertions;
using Opus.Engine.Ui.Direct3D12.Text;
using Xunit;

namespace Opus.Engine.Ui.Direct3D12.Tests.Text;

/// <summary>Headless guarantees for the bundled Roboto face (ADR-0034). No GPU: the font is read
/// from the assembly's manifest resource and rasterised on the CPU through stb_truetype, so these
/// run everywhere and pin the deterministic-Cyrillic promise independent of any system font.</summary>
public sealed class BundledFontsTests
{
    [Fact]
    public void Latin_face_opens_from_the_embedded_resource()
    {
        using var face = BundledFonts.TryOpenLatinFace();

        face.Should().NotBeNull("Roboto is embedded in the assembly and must always open");
    }

    [Theory]
    [InlineData('A')]   // Basic Latin
    [InlineData('z')]
    [InlineData('0')]
    [InlineData(',')]
    [InlineData('Д')]   // Cyrillic Д — the deterministic-Cyrillic guarantee
    [InlineData('я')]   // Cyrillic я
    [InlineData('Ж')]   // Cyrillic Ж
    public void Bundled_roboto_carries_the_glyph(int codepoint)
    {
        using var face = BundledFonts.TryOpenLatinFace();

        face!.HasGlyph(codepoint).Should().BeTrue();
    }

    [Fact]
    public void Bundled_roboto_rasterizes_a_cyrillic_glyph_to_real_coverage()
    {
        using var face = BundledFonts.TryOpenLatinFace();

        var raster = face!.Rasterize('Д', pixelHeight: 32f);   // Cyrillic Д

        raster.HasRaster.Should().BeTrue("a Cyrillic capital De must produce real coverage pixels");
        raster.Advance.Should().BeGreaterThan(0f);
    }
}
