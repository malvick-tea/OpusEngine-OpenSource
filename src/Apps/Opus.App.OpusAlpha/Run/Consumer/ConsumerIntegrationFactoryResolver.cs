using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Opus.Engine.Consumer.Integration;

namespace Opus.App.OpusAlpha.Run.Consumer;

/// <summary>
/// Resolves the single <see cref="IConsumerIntegrationFactory"/> from a loaded plugin
/// assembly's candidate types, constructs it, and builds its <see cref="ConsumerIntegration"/>.
/// Pure with respect to IO — it operates on a supplied type list, so the discovery and
/// construction rules are unit-tested without loading an assembly from disk. The disk and
/// <see cref="System.Runtime.Loader.AssemblyLoadContext"/> mechanics live in
/// <see cref="ConsumerIntegrationAssemblyLoader"/>.
/// </summary>
public static class ConsumerIntegrationFactoryResolver
{
    /// <summary>
    /// Selects the lone usable factory among <paramref name="candidateTypes"/> and returns its
    /// constructed integration, or an actionable failure when none, several, or a throwing
    /// factory is found. <paramref name="assemblyDescription"/> names the source in messages.
    /// </summary>
    public static ConsumerIntegrationLoadResult Resolve(
        IReadOnlyList<Type> candidateTypes,
        string assemblyDescription)
    {
        ArgumentNullException.ThrowIfNull(candidateTypes);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyDescription);

        var factories = candidateTypes.Where(IsUsableFactory).ToList();
        if (factories.Count == 0)
        {
            return ConsumerIntegrationLoadResult.Failure(
                $"Consumer assembly '{assemblyDescription}' exposes no public {nameof(IConsumerIntegrationFactory)} "
                + "with a public parameterless constructor.");
        }

        if (factories.Count > 1)
        {
            var names = string.Join(", ", factories.Select(static type => type.FullName));
            return ConsumerIntegrationLoadResult.Failure(
                $"Consumer assembly '{assemblyDescription}' exposes {factories.Count} "
                + $"{nameof(IConsumerIntegrationFactory)} implementations ({names}); exactly one is required.");
        }

        return Instantiate(factories[0], assemblyDescription);
    }

    private static bool IsUsableFactory(Type type) =>
        type is { IsClass: true, IsAbstract: false, IsPublic: true }
        && typeof(IConsumerIntegrationFactory).IsAssignableFrom(type)
        && type.GetConstructor(Type.EmptyTypes) is not null;

    private static ConsumerIntegrationLoadResult Instantiate(Type factoryType, string assemblyDescription)
    {
        // The factory and CreateIntegration run untrusted external code. Both are guarded so a
        // throwing plugin becomes a reported load failure instead of crashing the host — the
        // recovery contract that justifies catching broadly at this boundary.
        IConsumerIntegrationFactory factory;
        try
        {
            factory = (IConsumerIntegrationFactory)Activator.CreateInstance(factoryType)!;
        }
        catch (Exception ex)
        {
            return ConsumerIntegrationLoadResult.Failure(
                $"Consumer factory '{factoryType.FullName}' in '{assemblyDescription}' threw while "
                + $"constructing: {Describe(ex)}");
        }

        ConsumerIntegration? integration;
        try
        {
            integration = factory.CreateIntegration();
        }
        catch (Exception ex)
        {
            return ConsumerIntegrationLoadResult.Failure(
                $"Consumer factory '{factoryType.FullName}' threw from "
                + $"{nameof(IConsumerIntegrationFactory.CreateIntegration)}: {Describe(ex)}");
        }

        return integration is null
            ? ConsumerIntegrationLoadResult.Failure(
                $"Consumer factory '{factoryType.FullName}' returned a null integration.")
            : ConsumerIntegrationLoadResult.Success(integration);
    }

    private static string Describe(Exception exception) =>
        exception is TargetInvocationException { InnerException: { } inner }
            ? $"{inner.GetType().Name}: {inner.Message}"
            : $"{exception.GetType().Name}: {exception.Message}";
}
