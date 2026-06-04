namespace Opus.Engine.Net.Soak;

/// <summary>
/// Per-peer outcome from a <see cref="NetSoakHarness"/> run.
/// </summary>
/// <param name="PeerIndex">Zero-based index into the profile's peer cohort.</param>
/// <param name="Connected">Whether the peer reached <c>Connected</c> during the run.</param>
/// <param name="PacketsSent">Number of workload datagrams the harness handed to this
/// peer's transport (server-accepted or not).</param>
/// <param name="PacketsServerReceived">Number of datagrams the server actually observed
/// from this peer.</param>
/// <param name="PacketsEchoReceived">Number of echo datagrams the client received back
/// when <see cref="NetSoakProfile.EchoFromServer"/> is true; always 0 otherwise.</param>
/// <param name="CorruptPacketsObserved">Number of datagrams whose
/// <see cref="NetSoakPacketHeader"/> failed to decode against this peer.</param>
public sealed record NetSoakPeerReport(
    int PeerIndex,
    bool Connected,
    int PacketsSent,
    int PacketsServerReceived,
    int PacketsEchoReceived,
    int CorruptPacketsObserved);
