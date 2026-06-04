using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Opus.App.OpusAlpha.Run.Consumer;

/// <summary>
/// Loads a <see cref="Opus.Engine.Consumer.Integration.ConsumerIntegration"/> from an external
/// assembly path supplied on the alpha-host command line (<c>--consumer</c>). Validates the path,
/// loads the assembly through an isolating <see cref="ConsumerPluginLoadContext"/>, and delegates
/// the factory discovery / construction to <see cref="ConsumerIntegrationFactoryResolver"/>. Every
/// failure on this untrusted-file boundary is returned as an actionable
/// <see cref="ConsumerIntegrationLoadResult"/> rather than thrown.
/// </summary>
public static class ConsumerIntegrationAssemblyLoader
{
    /// <summary>Loads and constructs the consumer integration declared by the assembly at
    /// <paramref name="assemblyPath"/>, or returns a failure describing why it could not.</summary>
    public static ConsumerIntegrationLoadResult Load(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            return ConsumerIntegrationLoadResult.Failure("No consumer assembly path was supplied.");
        }

        if (!TryResolveFullPath(assemblyPath, out var fullPath, out var pathFailure))
        {
            return pathFailure!;
        }

        if (!File.Exists(fullPath))
        {
            return ConsumerIntegrationLoadResult.Failure($"Consumer assembly '{fullPath}' was not found.");
        }

        Assembly assembly;
        try
        {
            var context = new ConsumerPluginLoadContext(fullPath);
            assembly = context.LoadFromAssemblyPath(fullPath);
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException)
        {
            return ConsumerIntegrationLoadResult.Failure(
                $"Consumer assembly '{fullPath}' is not a loadable managed assembly: {ex.Message}");
        }

        return ConsumerIntegrationFactoryResolver.Resolve(GetLoadableTypes(assembly), Path.GetFileName(fullPath));
    }

    private static bool TryResolveFullPath(string assemblyPath, out string fullPath, out ConsumerIntegrationLoadResult? failure)
    {
        try
        {
            fullPath = Path.GetFullPath(assemblyPath);
            failure = null;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            fullPath = string.Empty;
            failure = ConsumerIntegrationLoadResult.Failure(
                $"Consumer assembly path '{assemblyPath}' is not a valid path: {ex.Message}");
            return false;
        }
    }

    private static IReadOnlyList<Type> GetLoadableTypes(Assembly assembly)
    {
        // A plugin assembly may carry types whose own dependencies cannot resolve; reflecting over
        // those throws. Keep the types that did load so a single resolvable factory is still found.
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(static type => type is not null).Cast<Type>().ToArray();
        }
    }
}
