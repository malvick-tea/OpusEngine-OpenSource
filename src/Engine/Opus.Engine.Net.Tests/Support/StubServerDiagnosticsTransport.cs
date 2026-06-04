using System;
using System.Collections.Generic;
using Opus.Net.Transport;

namespace Opus.Engine.Net.Tests.Support;

/// <summary>
/// Minimal server-side <see cref="INetTransport"/> that also implements
/// <see cref="INetServerTransportDiagnostics"/> with caller-set guard counts, so a
/// <see cref="Session.NetSession"/> test can assert the counts fold into the statistics snapshot
/// without standing up real sockets. The transport carries no traffic: <see cref="Poll"/> never
/// produces events and <see cref="Send"/> always refuses.
/// </summary>
internal sealed class StubServerDiagnosticsTransport : INetTransport, INetServerTransportDiagnostics
{
    public string Name => "stub-server-diagnostics";

    public bool IsOpen => true;

    public long RejectedConnectionCount { get; init; }

    public long DroppedInboundPayloadCount { get; init; }

    public long RateLimitedInboundPayloadCount { get; init; }

    public bool Send(ConnectionId target, ReadOnlySpan<byte> payload) => false;

    public void Poll(List<NetEvent> into) => into.Clear();

    public void Disconnect(ConnectionId target)
    {
    }

    public void Dispose()
    {
    }
}
