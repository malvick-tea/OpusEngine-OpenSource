using Opus.Foundation;

namespace Opus.Persistence;

/// <summary>
/// Replay file prologue. Captures everything needed to re-create the same World deterministically:
/// the seed, the mission id, the tick rate, and the authoring app version.
///
/// Body is a stream of <c>(Tick, PlayerInput[])</c> blocks, delta-encoded — see
/// tech-spec §5.7. One hour of play ≈ 200 KB.
/// </summary>
public readonly record struct ReplayHeader(
    string Magic,
    int SchemaVersion,
    AppVersion AuthoringVersion,
    string MissionId,
    ulong MatchSeed,
    int TickRateHz,
    long RecordedAtUnixMs,
    string PlayerProfileId)
{
    public const string MagicV1 = "GRPRPL1";

    public static ReplayHeader Current(
        int schemaVersion,
        AppVersion appVersion,
        string missionId,
        ulong matchSeed,
        int tickRateHz,
        long unixMs,
        string playerProfileId) => new(
            MagicV1, schemaVersion, appVersion, missionId, matchSeed, tickRateHz, unixMs, playerProfileId);

    public bool IsRecognisedMagic => Magic == MagicV1;
}
