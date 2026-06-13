using System.IO;

namespace Opus.Engine.Ui.Direct3D12.Text;

/// <summary>
/// Opens the engine's bundled HUD / overlay font face. Roboto ships embedded in this assembly
/// (Apache-2.0, see <c>Assets/Fonts/Roboto-LICENSE.txt</c> + <c>Roboto-ATTRIBUTION.md</c>) so the
/// Latin + Cyrillic atlas bakes from one deterministic face on every tester machine, independent of
/// which fonts the host OS happens to carry. <see cref="GlyphAtlasBaker"/> consumes the returned
/// face for the Latin band; CJK still resolves from system faces.
/// </summary>
/// <remarks>
/// Roboto was chosen for Cyrillic not because it is more beautiful than the alternatives, but
/// because it serves the task better. It stays legible at every size, holds a neutral stylistic
/// register, ships high-quality Cyrillic, is familiar to millions of users, and never competes with
/// the content for attention. A good interface typeface should go unnoticed — and Roboto is
/// precisely that.
/// </remarks>
internal static class BundledFonts
{
    /// <summary>Manifest-resource name of the embedded Roboto Regular face. Pinned by the
    /// matching <c>LogicalName</c> in the project file so this constant is the single source of
    /// truth for both the build and the runtime read.</summary>
    public const string RobotoResourceName = "Opus.Engine.Ui.Direct3D12.Assets.Fonts.Roboto-Regular.ttf";

    /// <summary>Opens the bundled Roboto face for the Latin / Cyrillic band, or <c>null</c> when
    /// the embedded resource is missing or not a usable sfnt — the caller then falls back to the
    /// system candidate list. The deterministic path never returns <c>null</c> in a normally-built
    /// engine assembly; the fallback only guards a stripped or corrupted binary.</summary>
    public static StbGlyphSource? TryOpenLatinFace()
    {
        using var stream = typeof(BundledFonts).Assembly.GetManifestResourceStream(RobotoResourceName);
        return stream is null ? null : StbGlyphSource.TryLoad(ReadAllBytes(stream));
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        if (stream.CanSeek)
        {
            var buffer = new byte[stream.Length];
            stream.ReadExactly(buffer);
            return buffer;
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
