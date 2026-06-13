using System;

namespace Opus.Engine.Net.Soak;

/// <summary>
/// Workload description for a <see cref="NetSoakHarness"/> run. All knobs are data; the
/// harness reads them once and never mutates the record.
/// </summary>
/// <param name="PeerCount">Number of simulated clients. Must be at least 1; the soak
/// roadmap target is 20-player-style stress so values up to 20+ are expected.</param>
/// <param name="PacketsPerPeer">Number of payload datagrams each client sends to the
/// server.</param>
/// <param name="PayloadBytes">Inline body bytes per datagram (header is added separately).</param>
/// <param name="EchoFromServer">When true, the server transport echoes every received
/// payload back to the originating peer. Lets a client-side path verify round-trip
/// arrival and detect server-side drops.</param>
/// <param name="ConnectBudget">Maximum wall-clock time the harness waits for every peer
/// to reach <c>Connected</c> before flagging missing peers.</param>
/// <param name="WorkloadBudget">Maximum wall-clock time the harness waits for the
/// workload to drain (every expected packet observed) before flagging missing packets
/// and stopping.</param>
public sealed record NetSoakProfile(
    int PeerCount,
    int PacketsPerPeer,
    int PayloadBytes,
    bool EchoFromServer,
    TimeSpan ConnectBudget,
    TimeSpan WorkloadBudget)
{
    /// <summary>Minimum allowed payload body. Zero is rejected because the soak shape
    /// needs at least one byte to differentiate empty heartbeats from workload datagrams.</summary>
    public const int MinimumPayloadBytes = 1;

    /// <summary>Maximum payload body. Sized against <see cref="ushort"/> minus the
    /// 16-byte soak header so the workload fits inside a single UDP datagram with room
    /// for the transport's own framing.</summary>
    public const int MaximumPayloadBytes = ushort.MaxValue - NetSoakPacketHeader.SizeBytes - 64;

    /// <summary>Default test-scale profile: a 4-peer cohort with 64 packets each,
    /// 256-byte bodies, server echo enabled, sized so it completes inside ~2 seconds on
    /// loopback hardware. Production soak runs should construct a larger profile that
    /// matches the 20-player target.</summary>
    public static NetSoakProfile Default { get; } = new(
        PeerCount: 4,
        PacketsPerPeer: 64,
        PayloadBytes: 256,
        EchoFromServer: true,
        ConnectBudget: TimeSpan.FromSeconds(3),
        WorkloadBudget: TimeSpan.FromSeconds(5));

    /// <summary>Throws when the profile is internally inconsistent.</summary>
    public void Validate()
    {
        if (PeerCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(PeerCount), "PeerCount must be at least 1.");
        }

        if (PacketsPerPeer < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(PacketsPerPeer), "PacketsPerPeer must be at least 1.");
        }

        if (PayloadBytes < MinimumPayloadBytes || PayloadBytes > MaximumPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PayloadBytes),
                $"PayloadBytes must be between {MinimumPayloadBytes} and {MaximumPayloadBytes}.");
        }

        if (ConnectBudget <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ConnectBudget), "ConnectBudget must be positive.");
        }

        if (WorkloadBudget <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(WorkloadBudget), "WorkloadBudget must be positive.");
        }
    }

    /// <summary>Total payload bytes the workload should exchange in one direction. Used
    /// by callers that want to size a reporting threshold.</summary>
    public long TotalExpectedBytes => (long)PeerCount * PacketsPerPeer * PayloadBytes;
}
