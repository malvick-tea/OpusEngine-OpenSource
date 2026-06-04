using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Ordered key/value text metadata attached to a PNG screenshot via
/// PNG tEXt chunks. Used to embed build identity, adapter name, frame index, and any
/// other ASCII-only diagnostics directly inside the image file so a screenshot sent by
/// a tester is self-describing.
/// <para>
/// Keys must be ASCII, 1–79 characters, with no leading/trailing whitespace and no
/// embedded control characters — the PNG spec restricts tEXt keywords to that range
/// and the writer rejects anything outside it instead of silently corrupting the file.
/// Values are restricted to ISO-8859-1 (Latin-1) per the same spec. Unicode payloads
/// should be encoded ahead of time or live in iTXt (out of scope for this minimal
/// writer).
/// </para>
/// </summary>
public sealed record D3D12ScreenshotMetadata(ImmutableArray<D3D12ScreenshotMetadataEntry> Entries)
{
    public static readonly D3D12ScreenshotMetadata Empty =
        new(ImmutableArray<D3D12ScreenshotMetadataEntry>.Empty);

    public int Count => Entries.Length;

    public static D3D12ScreenshotMetadata From(IEnumerable<KeyValuePair<string, string>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var builder = ImmutableArray.CreateBuilder<D3D12ScreenshotMetadataEntry>();
        foreach (var pair in entries)
        {
            builder.Add(D3D12ScreenshotMetadataEntry.Create(pair.Key, pair.Value));
        }

        return new D3D12ScreenshotMetadata(builder.ToImmutable());
    }
}

/// <summary>A single PNG tEXt entry. Use <see cref="Create"/> to validate keyword and
/// value against the PNG spec — invalid input throws a named exception instead of
/// producing a corrupt PNG.</summary>
public sealed record D3D12ScreenshotMetadataEntry(string Keyword, string Value)
{
    private const int KeywordMinLength = 1;
    private const int KeywordMaxLength = 79;

    public static D3D12ScreenshotMetadataEntry Create(string keyword, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyword);
        ArgumentNullException.ThrowIfNull(value);

        if (keyword.Length < KeywordMinLength || keyword.Length > KeywordMaxLength)
        {
            throw new ArgumentException(
                $"PNG tEXt keyword must be {KeywordMinLength}–{KeywordMaxLength} characters; got {keyword.Length}.",
                nameof(keyword));
        }

        if (keyword[0] == ' ' || keyword[^1] == ' ')
        {
            throw new ArgumentException(
                "PNG tEXt keyword must not start or end with a space.",
                nameof(keyword));
        }

        foreach (var ch in keyword)
        {
            if (ch < 32 || ch > 126)
            {
                throw new ArgumentException(
                    "PNG tEXt keyword must be printable ASCII (32–126).",
                    nameof(keyword));
            }
        }

        foreach (var ch in value)
        {
            if (ch > 0xFF)
            {
                throw new ArgumentException(
                    "PNG tEXt value must be Latin-1 (0–255). Use iTXt for arbitrary Unicode.",
                    nameof(value));
            }
        }

        return new D3D12ScreenshotMetadataEntry(keyword, value);
    }
}
