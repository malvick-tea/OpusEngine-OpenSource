using System;
using System.Threading;
using System.Threading.Tasks;

namespace Opus.Engine.Pal.Networking;

/// <summary>
/// UDP / TCP transport contract for in-match net (rollback netcode in M5+).
/// Stays at the Pal layer because the actual socket APIs differ across platforms
/// (especially iOS background-mode rules and Android NDK quirks).
/// </summary>
public interface ISocketTransport : IDisposable
{
    SocketKind Kind { get; }

    bool IsBound { get; }

    Task BindAsync(string localAddress, int localPort, CancellationToken ct);

    Task SendAsync(ReadOnlyMemory<byte> payload, string remoteAddress, int remotePort, CancellationToken ct);

    /// <summary>Receives next datagram or stream chunk. Buffer ownership transfers to caller until next call.</summary>
    ValueTask<TransportPacket> ReceiveAsync(CancellationToken ct);
}

public enum SocketKind
{
    Udp,
    Tcp,
    Quic,
}

public readonly record struct TransportPacket(
    ReadOnlyMemory<byte> Payload,
    string RemoteAddress,
    int RemotePort);
