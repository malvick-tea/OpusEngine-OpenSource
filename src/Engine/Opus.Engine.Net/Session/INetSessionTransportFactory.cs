using Opus.Net.Transport;

namespace Opus.Engine.Net.Session;

/// <summary>
/// Creates the underlying <see cref="INetTransport"/> instance for a client
/// <see cref="NetSession"/>. The factory is invoked once on <see cref="NetSession.Start"/>
/// and again on every reconnect attempt so a fresh socket/handshake is exercised end
/// to end. Server sessions ignore the factory: the bound socket is supplied directly
/// through <see cref="NetSession.AdoptServer"/>.
/// </summary>
public interface INetSessionTransportFactory
{
    /// <summary>Creates a brand new transport instance ready to accept poll/send. The
    /// returned transport's lifetime is managed by the session — the session disposes
    /// every transport it creates.</summary>
    INetTransport Create();
}
