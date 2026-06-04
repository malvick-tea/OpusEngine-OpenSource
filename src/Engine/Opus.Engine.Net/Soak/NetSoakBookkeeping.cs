using System;
using System.Collections.Generic;
using System.Globalization;
using Opus.Engine.Net.Session;
using Opus.Net.Transport;

namespace Opus.Engine.Net.Soak;

/// <summary>
/// Mutable per-run accumulator for <see cref="NetSoakHarness"/>. Kept in its own type so
/// the harness orchestrator stays a stateless pipeline and every counter mutation has a
/// single home. Single-threaded by design: the harness drives it from one thread.
/// </summary>
internal sealed class NetSoakBookkeeping
{
    private readonly PeerSlot[] _peers;
    private readonly List<NetSoakIssue> _issues = new();
    private readonly HashSet<ConnectionId> _acceptedServerPeers = new();

    private NetSoakBookkeeping(PeerSlot[] peers)
    {
        _peers = peers;
    }

    public static NetSoakBookkeeping ForProfile(NetSoakProfile profile)
    {
        var peers = new PeerSlot[profile.PeerCount];
        for (var i = 0; i < peers.Length; i++)
        {
            peers[i] = new PeerSlot();
        }

        return new NetSoakBookkeeping(peers);
    }

    public bool AllPeersConnected
    {
        get
        {
            foreach (var peer in _peers)
            {
                if (!peer.ClientConnected)
                {
                    return false;
                }
            }

            return _acceptedServerPeers.Count >= _peers.Length;
        }
    }

    public bool IsPeerConnected(int peerIndex) =>
        _peers[peerIndex].ClientConnected;

    public void RecordServerAccepted(ConnectionId connection) =>
        _acceptedServerPeers.Add(connection);

    public void RecordClientConnected(int peerIndex) =>
        _peers[peerIndex].ClientConnected = true;

    public void RecordClientSend(int peerIndex) =>
        _peers[peerIndex].PacketsSent++;

    public void RecordServerReceived(int peerIndex, int sequenceNumber)
    {
        if (peerIndex < 0 || peerIndex >= _peers.Length)
        {
            _issues.Add(NetSoakIssue.Global(
                NetSoakIssueCode.PayloadCorruption,
                $"server received header.peerIndex={peerIndex} outside cohort",
                DateTimeOffset.UtcNow));
            return;
        }

        _peers[peerIndex].ServerSequencesReceived.Add(sequenceNumber);
    }

    public void RecordEchoReceived(int peerIndex, int sequenceNumber) =>
        _peers[peerIndex].EchoSequencesReceived.Add(sequenceNumber);

    public void RecordCorruptObservation(int peerIndex) =>
        _peers[peerIndex].CorruptObservations++;

    public void RecordIssue(NetSoakIssue issue) => _issues.Add(issue);

    public bool IsWorkloadComplete(NetSoakProfile profile)
    {
        for (var i = 0; i < _peers.Length; i++)
        {
            var slot = _peers[i];
            if (slot.ServerSequencesReceived.Count < profile.PacketsPerPeer)
            {
                return false;
            }

            if (profile.EchoFromServer && slot.EchoSequencesReceived.Count < profile.PacketsPerPeer)
            {
                return false;
            }
        }

        return true;
    }

    public void RecordDropsForMissingPackets(NetSoakProfile profile, DateTimeOffset capturedAtUtc)
    {
        for (var i = 0; i < _peers.Length; i++)
        {
            var slot = _peers[i];
            for (var seq = 0; seq < profile.PacketsPerPeer; seq++)
            {
                if (!slot.ServerSequencesReceived.Contains(seq))
                {
                    _issues.Add(NetSoakIssue.ForPeer(
                        NetSoakIssueCode.PacketDropped,
                        i,
                        $"server missed packet seq={seq.ToString(CultureInfo.InvariantCulture)}",
                        capturedAtUtc));
                }

                if (profile.EchoFromServer && !slot.EchoSequencesReceived.Contains(seq))
                {
                    _issues.Add(NetSoakIssue.ForPeer(
                        NetSoakIssueCode.PacketDropped,
                        i,
                        $"client missed echo seq={seq.ToString(CultureInfo.InvariantCulture)}",
                        capturedAtUtc));
                }
            }
        }
    }

    public NetSoakReport BuildReport(NetSoakProfile profile, TimeSpan elapsed, NetTransportGuardCounts serverGuards)
    {
        var peerReports = new NetSoakPeerReport[_peers.Length];
        long totalServerReceived = 0;
        long totalEchoReceived = 0;
        long totalBytesSent = 0;
        long totalBytesServerReceived = 0;
        for (var i = 0; i < _peers.Length; i++)
        {
            var slot = _peers[i];
            peerReports[i] = new NetSoakPeerReport(
                PeerIndex: i,
                Connected: slot.ClientConnected,
                PacketsSent: slot.PacketsSent,
                PacketsServerReceived: slot.ServerSequencesReceived.Count,
                PacketsEchoReceived: slot.EchoSequencesReceived.Count,
                CorruptPacketsObserved: slot.CorruptObservations);
            totalServerReceived += slot.ServerSequencesReceived.Count;
            totalEchoReceived += slot.EchoSequencesReceived.Count;
            totalBytesSent += (long)slot.PacketsSent * profile.PayloadBytes;
            totalBytesServerReceived += (long)slot.ServerSequencesReceived.Count * profile.PayloadBytes;
        }

        return new NetSoakReport(
            Profile: profile,
            ElapsedWallClock: elapsed,
            Peers: peerReports,
            Issues: _issues.ToArray(),
            TotalPacketsExpected: profile.TotalExpectedBytes / profile.PayloadBytes,
            TotalPacketsServerReceived: totalServerReceived,
            TotalEchoPacketsReceived: totalEchoReceived,
            TotalBytesSent: totalBytesSent,
            TotalBytesServerReceived: totalBytesServerReceived,
            ServerGuards: serverGuards);
    }

    private sealed class PeerSlot
    {
        public bool ClientConnected { get; set; }

        public int PacketsSent { get; set; }

        public int CorruptObservations { get; set; }

        public HashSet<int> ServerSequencesReceived { get; } = new();

        public HashSet<int> EchoSequencesReceived { get; } = new();
    }
}
