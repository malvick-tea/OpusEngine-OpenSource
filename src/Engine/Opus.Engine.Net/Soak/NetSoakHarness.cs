using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Opus.Engine.Net.Session;
using Opus.Net.Transport;

namespace Opus.Engine.Net.Soak;

/// <summary>
/// Transport-agnostic soak harness. Drives a fixed workload (handshake → per-peer send
/// burst → drain) over any <see cref="INetSoakRig"/> implementation and returns a
/// structured <see cref="NetSoakReport"/> describing what was sent, received, dropped,
/// echoed, or corrupted. Owns no state of its own — the harness is a pure orchestrator;
/// the rig owns the transports.
/// <para>
/// The harness is single-threaded: it busy-polls server and client transports between
/// brief sleeps so loopback and real-UDP implementations can drive their own background
/// workers. Per ADR-0007 / ADR-0013, the harness does not introduce a game-side wire
/// shape — workload datagrams carry only <see cref="NetSoakPacketHeader"/> + opaque
/// bytes.
/// </para>
/// </summary>
public static class NetSoakHarness
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(5);

    /// <summary>Runs the workload described by <paramref name="profile"/> against
    /// <paramref name="rig"/>. Returns a structured report; never throws for a failing
    /// run (issues are recorded inside the report).</summary>
    public static NetSoakReport Run(
        NetSoakProfile profile,
        INetSoakRig rig,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(rig);
        profile.Validate();
        if (rig.PeerCount != profile.PeerCount)
        {
            throw new ArgumentException(
                $"Rig peer count ({rig.PeerCount}) does not match profile ({profile.PeerCount}).",
                nameof(rig));
        }

        var clock = time ?? TimeProvider.System;
        var stopwatch = Stopwatch.StartNew();

        var bookkeeping = NetSoakBookkeeping.ForProfile(profile);
        WaitForConnections(profile, rig, bookkeeping, clock);
        RunWorkload(profile, rig, bookkeeping, clock);
        WaitForDrain(profile, rig, bookkeeping, clock);

        stopwatch.Stop();
        var serverGuards = NetTransportGuardCounts.FromTransport(rig.Server);
        return bookkeeping.BuildReport(profile, stopwatch.Elapsed, serverGuards);
    }

    private static void WaitForConnections(
        NetSoakProfile profile,
        INetSoakRig rig,
        NetSoakBookkeeping bookkeeping,
        TimeProvider clock)
    {
        var deadline = clock.GetUtcNow() + profile.ConnectBudget;
        var serverEvents = new List<NetEvent>();
        var clientEvents = new List<NetEvent>();
        while (!bookkeeping.AllPeersConnected && clock.GetUtcNow() < deadline)
        {
            PollServer(rig, serverEvents, bookkeeping, clock, profile.EchoFromServer);
            for (var peerIndex = 0; peerIndex < profile.PeerCount; peerIndex++)
            {
                PollClient(rig, peerIndex, clientEvents, bookkeeping, clock);
            }

            if (bookkeeping.AllPeersConnected)
            {
                break;
            }

            Thread.Sleep(PollInterval);
        }

        for (var peerIndex = 0; peerIndex < profile.PeerCount; peerIndex++)
        {
            if (!bookkeeping.IsPeerConnected(peerIndex))
            {
                bookkeeping.RecordIssue(NetSoakIssue.ForPeer(
                    NetSoakIssueCode.PeerUnconnected,
                    peerIndex,
                    $"peer {peerIndex} did not observe Connected inside ConnectBudget",
                    clock.GetUtcNow()));
            }
        }
    }

    private static void RunWorkload(
        NetSoakProfile profile,
        INetSoakRig rig,
        NetSoakBookkeeping bookkeeping,
        TimeProvider clock)
    {
        Span<byte> scratch = stackalloc byte[Math.Min(profile.PayloadBytes + NetSoakPacketHeader.SizeBytes, 16_384)];
        for (var peerIndex = 0; peerIndex < profile.PeerCount; peerIndex++)
        {
            if (!bookkeeping.IsPeerConnected(peerIndex))
            {
                continue;
            }

            var client = rig.Client(peerIndex);
            for (var seq = 0; seq < profile.PacketsPerPeer; seq++)
            {
                var header = new NetSoakPacketHeader((uint)peerIndex, (uint)seq, (uint)profile.PayloadBytes);
                Span<byte> buffer = profile.PayloadBytes + NetSoakPacketHeader.SizeBytes <= scratch.Length
                    ? scratch[..(NetSoakPacketHeader.SizeBytes + profile.PayloadBytes)]
                    : new byte[NetSoakPacketHeader.SizeBytes + profile.PayloadBytes];
                FillDeterministicPayload(buffer[NetSoakPacketHeader.SizeBytes..], peerIndex, seq);
                NetSoakPacketHeader.Encode(header, buffer[NetSoakPacketHeader.SizeBytes..], buffer);
                if (!client.Send(rig.ServerSentinel, buffer))
                {
                    bookkeeping.RecordIssue(NetSoakIssue.ForPeer(
                        NetSoakIssueCode.TransportFault,
                        peerIndex,
                        $"client transport refused packet {seq}",
                        clock.GetUtcNow()));
                    continue;
                }

                bookkeeping.RecordClientSend(peerIndex);
            }
        }
    }

    private static void WaitForDrain(
        NetSoakProfile profile,
        INetSoakRig rig,
        NetSoakBookkeeping bookkeeping,
        TimeProvider clock)
    {
        var deadline = clock.GetUtcNow() + profile.WorkloadBudget;
        var serverEvents = new List<NetEvent>();
        var clientEvents = new List<NetEvent>();
        while (clock.GetUtcNow() < deadline)
        {
            PollServer(rig, serverEvents, bookkeeping, clock, profile.EchoFromServer);
            for (var peerIndex = 0; peerIndex < profile.PeerCount; peerIndex++)
            {
                PollClient(rig, peerIndex, clientEvents, bookkeeping, clock);
            }

            if (bookkeeping.IsWorkloadComplete(profile))
            {
                return;
            }

            Thread.Sleep(PollInterval);
        }

        if (!bookkeeping.IsWorkloadComplete(profile))
        {
            bookkeeping.RecordIssue(NetSoakIssue.Global(
                NetSoakIssueCode.BudgetExceeded,
                $"workload budget {profile.WorkloadBudget.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)}ms elapsed before drain completed",
                clock.GetUtcNow()));
            bookkeeping.RecordDropsForMissingPackets(profile, clock.GetUtcNow());
        }
    }

    private static void PollServer(
        INetSoakRig rig,
        List<NetEvent> buffer,
        NetSoakBookkeeping bookkeeping,
        TimeProvider clock,
        bool echo)
    {
        try
        {
            rig.Server.Poll(buffer);
        }
        catch (Exception ex)
        {
            bookkeeping.RecordIssue(NetSoakIssue.Global(
                NetSoakIssueCode.TransportFault,
                $"server poll threw: {ex.GetType().Name}",
                clock.GetUtcNow()));
            return;
        }

        foreach (var transportEvent in buffer)
        {
            HandleServerEvent(rig, transportEvent, bookkeeping, clock, echo);
        }
    }

    private static void HandleServerEvent(
        INetSoakRig rig,
        NetEvent transportEvent,
        NetSoakBookkeeping bookkeeping,
        TimeProvider clock,
        bool echo)
    {
        if (transportEvent.Kind == NetEventKind.Connected)
        {
            bookkeeping.RecordServerAccepted(transportEvent.Connection);
            return;
        }

        if (transportEvent.Kind != NetEventKind.Received)
        {
            return;
        }

        if (!NetSoakPacketHeader.TryDecode(transportEvent.Payload, out var header, out var payload))
        {
            bookkeeping.RecordIssue(NetSoakIssue.Global(
                NetSoakIssueCode.PayloadCorruption,
                "server observed datagram with unrecognized soak header",
                clock.GetUtcNow()));
            return;
        }

        bookkeeping.RecordServerReceived((int)header.PeerIndex, (int)header.SequenceNumber);

        if (echo)
        {
            Span<byte> echoBuffer = stackalloc byte[NetSoakPacketHeader.SizeBytes + 1024];
            var totalBytes = NetSoakPacketHeader.SizeBytes + payload.Length;
            Span<byte> outBuffer = totalBytes <= echoBuffer.Length ? echoBuffer[..totalBytes] : new byte[totalBytes];
            NetSoakPacketHeader.Encode(header, payload, outBuffer);
            rig.Server.Send(transportEvent.Connection, outBuffer);
        }
    }

    private static void PollClient(
        INetSoakRig rig,
        int peerIndex,
        List<NetEvent> buffer,
        NetSoakBookkeeping bookkeeping,
        TimeProvider clock)
    {
        try
        {
            rig.Client(peerIndex).Poll(buffer);
        }
        catch (Exception ex)
        {
            bookkeeping.RecordIssue(NetSoakIssue.ForPeer(
                NetSoakIssueCode.TransportFault,
                peerIndex,
                $"client poll threw: {ex.GetType().Name}",
                clock.GetUtcNow()));
            return;
        }

        foreach (var transportEvent in buffer)
        {
            if (transportEvent.Kind == NetEventKind.Connected)
            {
                bookkeeping.RecordClientConnected(peerIndex);
                continue;
            }

            if (transportEvent.Kind != NetEventKind.Received)
            {
                continue;
            }

            if (!NetSoakPacketHeader.TryDecode(transportEvent.Payload, out var header, out _))
            {
                bookkeeping.RecordCorruptObservation(peerIndex);
                continue;
            }

            bookkeeping.RecordEchoReceived(peerIndex, (int)header.SequenceNumber);
        }
    }

    private static void FillDeterministicPayload(Span<byte> destination, int peerIndex, int sequence)
    {
        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = (byte)((peerIndex * 31) + sequence + i);
        }
    }
}
