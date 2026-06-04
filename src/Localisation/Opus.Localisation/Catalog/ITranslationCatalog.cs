using System.Collections.Generic;
using Opus.Foundation;

namespace Opus.Localisation;

/// <summary>
/// Read-only translation catalogue for a single locale snapshot.
/// Hosts (Client/Tools) load CSVs/baked .gloc files and adapt them to this interface.
/// </summary>
public interface ITranslationCatalog
{
    string Locale { get; }

    /// <summary>Returns the localised string, or the key itself if missing (visible regression).</summary>
    string Get(TranslationKey key);

    Result<string> TryGet(TranslationKey key);

    bool Has(TranslationKey key);

    IReadOnlyCollection<string> AllKeys { get; }
}
