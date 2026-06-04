using System;
using Opus.Engine.Net.Session;
using Opus.Net.Loopback;
using Opus.Net.Transport;

namespace Opus.Engine.Net.Tests.Support;

/// <summary>Test factory that hands a fresh client side of a
/// <see cref="LoopbackTransportPair"/> to a <see cref="NetSession"/> on every
/// <see cref="Create"/>. Tracks the most recently issued link so tests can poke the
/// server side directly for handshake/disconnect orchestration.</summary>
internal sealed class LoopbackClientTransportFactory : INetSessionTransportFactory, IDisposable
{
    public LoopbackTransportLink? LastLink { get; private set; }

    public int CreateCallCount { get; private set; }

    public bool ThrowOnCreate { get; set; }

    public bool ReturnNullOnCreate { get; set; }

    public INetTransport Create()
    {
        CreateCallCount++;
        if (ThrowOnCreate)
        {
            throw new InvalidOperationException("factory configured to throw");
        }

        if (ReturnNullOnCreate)
        {
            return null!;
        }

        LastLink?.Server.Dispose();
        var link = LoopbackTransportPair.Create();
        LastLink = link;
        return link.Client;
    }

    public void Dispose()
    {
        LastLink?.Server.Dispose();
        LastLink = null;
    }
}
