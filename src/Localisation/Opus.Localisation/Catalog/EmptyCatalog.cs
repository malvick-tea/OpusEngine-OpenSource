using System;
using System.Collections.Generic;
using Opus.Foundation;

namespace Opus.Localisation;

/// <summary>
/// Sink-of-last-resort catalogue. Every <see cref="Get(TranslationKey)"/> returns the key
/// verbatim — useful as a default DI binding before the real catalogue is loaded, and as
/// a way to surface missing-translation regressions visually in dev builds.
/// </summary>
public sealed class EmptyCatalog : ITranslationCatalog
{
    public static readonly EmptyCatalog Instance = new();

    public string Locale => "void";

    public string Get(TranslationKey key) => key.Key;

    public Result<string> TryGet(TranslationKey key) =>
        Result<string>.Err(ErrorCode.TranslationKeyMissing, key.Key);

    public bool Has(TranslationKey key) => false;

    public IReadOnlyCollection<string> AllKeys { get; } = Array.Empty<string>();
}
