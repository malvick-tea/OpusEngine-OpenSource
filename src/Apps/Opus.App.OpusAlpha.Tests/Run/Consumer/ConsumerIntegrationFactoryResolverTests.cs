using System;
using FluentAssertions;
using Opus.App.OpusAlpha.Run.Consumer;
using Opus.Engine.Consumer.Integration;
using Opus.Engine.Consumer.Lifecycle;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Run.Consumer;

public sealed class ConsumerIntegrationFactoryResolverTests
{
    private const string AssemblyDescription = "consumer-fixture.dll";

    [Fact]
    public void Resolve_builds_the_integration_from_the_single_factory()
    {
        var result = ConsumerIntegrationFactoryResolver.Resolve(
            new[] { typeof(ResolverValidConsumerFactory) }, AssemblyDescription);

        result.Succeeded.Should().BeTrue();
        result.Integration.Should().NotBeNull();
        result.Integration!.HasContracts.Should().BeTrue();
        result.FailureReason.Should().BeNull();
    }

    [Fact]
    public void Resolve_fails_when_no_factory_is_present()
    {
        var result = ConsumerIntegrationFactoryResolver.Resolve(
            new[] { typeof(NotAConsumerFactory) }, AssemblyDescription);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Contain("no public");
    }

    [Fact]
    public void Resolve_fails_when_several_factories_are_present()
    {
        var result = ConsumerIntegrationFactoryResolver.Resolve(
            new[] { typeof(ResolverValidConsumerFactory), typeof(ResolverSecondConsumerFactory) },
            AssemblyDescription);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Contain("exactly one");
    }

    [Fact]
    public void Resolve_ignores_abstract_and_constructorless_factory_types()
    {
        var result = ConsumerIntegrationFactoryResolver.Resolve(
            new[] { typeof(AbstractConsumerFactory), typeof(NoParameterlessCtorConsumerFactory) },
            AssemblyDescription);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Contain("no public");
    }

    [Fact]
    public void Resolve_fails_when_the_factory_constructor_throws()
    {
        var result = ConsumerIntegrationFactoryResolver.Resolve(
            new[] { typeof(ThrowingCtorConsumerFactory) }, AssemblyDescription);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Contain("constructing");
    }

    [Fact]
    public void Resolve_fails_when_create_integration_throws()
    {
        var result = ConsumerIntegrationFactoryResolver.Resolve(
            new[] { typeof(ThrowingCreateConsumerFactory) }, AssemblyDescription);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Contain(nameof(IConsumerIntegrationFactory.CreateIntegration));
    }

    [Fact]
    public void Resolve_fails_when_create_integration_returns_null()
    {
        var result = ConsumerIntegrationFactoryResolver.Resolve(
            new[] { typeof(NullReturningConsumerFactory) }, AssemblyDescription);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Contain("null integration");
    }
}

public sealed class ResolverValidConsumerFactory : IConsumerIntegrationFactory
{
    public ConsumerIntegration CreateIntegration() => new(
        sceneSource: null,
        assetCatalog: null,
        telemetryProvider: null,
        lifecycleHooks: new IConsumerLifecycleHook[] { new ResolverNoOpLifecycleHook() });
}

public sealed class ResolverSecondConsumerFactory : IConsumerIntegrationFactory
{
    public ConsumerIntegration CreateIntegration() => ConsumerIntegration.Empty;
}

public sealed class NotAConsumerFactory
{
}

public abstract class AbstractConsumerFactory : IConsumerIntegrationFactory
{
    public abstract ConsumerIntegration CreateIntegration();
}

public sealed class NoParameterlessCtorConsumerFactory : IConsumerIntegrationFactory
{
    public NoParameterlessCtorConsumerFactory(int unused) => Unused = unused;

    public int Unused { get; }

    public ConsumerIntegration CreateIntegration() => ConsumerIntegration.Empty;
}

public sealed class ThrowingCtorConsumerFactory : IConsumerIntegrationFactory
{
    public ThrowingCtorConsumerFactory() => throw new InvalidOperationException("ctor boom");

    public ConsumerIntegration CreateIntegration() => ConsumerIntegration.Empty;
}

public sealed class ThrowingCreateConsumerFactory : IConsumerIntegrationFactory
{
    public ConsumerIntegration CreateIntegration() => throw new InvalidOperationException("create boom");
}

public sealed class NullReturningConsumerFactory : IConsumerIntegrationFactory
{
    public ConsumerIntegration CreateIntegration() => null!;
}

internal sealed class ResolverNoOpLifecycleHook : IConsumerLifecycleHook
{
    public void OnStarted(ConsumerLifecycleStartedContext context) => ArgumentNullException.ThrowIfNull(context);

    public void OnFrame(ConsumerFrameContext context) => ArgumentNullException.ThrowIfNull(context);

    public void OnStopping(ConsumerLifecycleStoppingContext context) => ArgumentNullException.ThrowIfNull(context);
}
