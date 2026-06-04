using System;
using FluentAssertions;
using Opus.Engine.Net.Session;
using Xunit;

namespace Opus.Engine.Net.Tests.Session;

/// <summary>Boundary validation for <see cref="NetSessionOptions"/>. The session
/// constructor calls Validate so these checks pin what bad options the engine rejects
/// before any transport is opened.</summary>
public sealed class NetSessionOptionsTests
{
    [Fact]
    public void Default_options_validate()
    {
        var options = new NetSessionOptions(NetSessionRole.Client, "ok");
        Action act = options.Validate;
        act.Should().NotThrow();
    }

    [Fact]
    public void Empty_DisplayName_is_rejected()
    {
        var options = new NetSessionOptions(NetSessionRole.Client, "  ");
        Action act = options.Validate;
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tiny_queue_is_rejected()
    {
        var options = new NetSessionOptions(NetSessionRole.Client, "ok", MaxQueuedPayloads: 1);
        Action act = options.Validate;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Server_role_skips_reconnect_validation()
    {
        // A nonsense reconnect policy is fine for a server session because reconnect is
        // never used on the server side; only client sessions touch the field.
        var options = new NetSessionOptions(
            NetSessionRole.Server,
            "ok",
            new NetReconnectPolicy(-1, TimeSpan.Zero, TimeSpan.Zero, 1.0));

        Action act = options.Validate;
        act.Should().NotThrow();
    }
}
