namespace Opus.Engine.Net.Soak;

/// <summary>
/// Stable classification of issues observed during a <see cref="NetSoakHarness"/> run.
/// Append-only; never renumber once shipped to a tester report.
/// </summary>
public enum NetSoakIssueCode
{
    /// <summary>A peer never transitioned to <c>Connected</c> before the run budget
    /// elapsed.</summary>
    PeerUnconnected = 1,

    /// <summary>A received payload's <see cref="NetSoakPacketHeader"/> failed to decode
    /// (bad magic, out-of-range length, etc.).</summary>
    PayloadCorruption = 2,

    /// <summary>A packet the harness expected to receive never arrived inside the run
    /// budget.</summary>
    PacketDropped = 3,

    /// <summary>The harness wall-clock budget elapsed before the workload finished.</summary>
    BudgetExceeded = 4,

    /// <summary>The underlying transport reported a fault during the run.</summary>
    TransportFault = 5,
}
