using System.IO;

namespace Opus.Engine.Ui.Direct3D12.Text;

/// <summary>
/// Opens an optional embedded HUD / overlay font face. Source-only copies do not ship font
/// assets, so callers also use <see cref="SystemFontLoader"/> for host font fallback.
/// </summary>
internal static class BundledFonts
{
    /// <summary>Manifest-resource name used when a host chooses to embed a Latin face.</summary>
    public const string RobotoResourceName = "Opus.Engine.Ui.Direct3D12.Assets.Fonts.Roboto-Regular.ttf";

    /// <summary>Opens the embedded Latin face, or <c>null</c> when the resource is absent.</summary>
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
