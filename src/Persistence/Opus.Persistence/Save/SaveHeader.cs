using Opus.Foundation;

namespace Opus.Persistence;

/// <summary>
/// Fixed-shape header that prefixes every save body. Detects format version mismatches
/// before we even attempt to deserialise the body.
///
/// <see cref="Magic"/> is the constant <c>"GRPSAV1"</c> — present so a corrupt or
/// foreign file is rejected immediately. <see cref="SchemaVersion"/> bumps on
/// breaking layout changes; the migration pipeline (planned: M3) reads this field
/// to decide which migrators to chain.
/// </summary>
public readonly record struct SaveHeader(
    string Magic,
    int SchemaVersion,
    AppVersion AuthoringVersion,
    long CreatedAtUnixMs)
{
    public const string MagicV1 = "GRPSAV1";

    public static SaveHeader Current(int schemaVersion, AppVersion appVersion, long unixMs) =>
        new(MagicV1, schemaVersion, appVersion, unixMs);

    public bool IsRecognisedMagic => Magic == MagicV1;
}
