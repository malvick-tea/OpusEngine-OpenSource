using System;
using System.Collections.Generic;
using System.IO;
using Opus.Engine.Ui.Text;

namespace Opus.Engine.Ui.Direct3D12.Text;

/// <summary>
/// Resolves the first usable font file from an ordered candidate list into an open
/// <see cref="StbGlyphSource"/>. A <c>.ttc</c> candidate is reduced to its first member
/// font through <see cref="TrueTypeCollection"/> before being handed to stb_truetype.
/// </summary>
internal static class SystemFontLoader
{
    private const string CollectionExtension = ".ttc";

    /// <summary>Walks <paramref name="candidatePaths"/> in order and returns the first
    /// font that exists and opens. Returns <c>null</c> when none are available — the
    /// caller decides whether that is fatal.</summary>
    public static StbGlyphSource? LoadFirstAvailable(IEnumerable<string> candidatePaths)
    {
        foreach (var path in candidatePaths)
        {
            var source = TryLoad(path);
            if (source is not null)
            {
                return source;
            }
        }

        return null;
    }

    private static StbGlyphSource? TryLoad(string path)
    {
        byte[] bytes;
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            bytes = File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        if (path.EndsWith(CollectionExtension, StringComparison.OrdinalIgnoreCase))
        {
            var sfnt = TrueTypeCollection.ExtractFont(bytes, 0);
            return sfnt is null ? null : StbGlyphSource.TryLoad(sfnt);
        }

        return StbGlyphSource.TryLoad(bytes);
    }
}
