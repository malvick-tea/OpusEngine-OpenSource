using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Opus.App.OpusAlpha.Run.Consumer;

/// <summary>
/// Loads an external consumer plugin assembly and its private dependencies in isolation from the
/// host, while sharing every assembly the host can already supply (the engine contract assemblies
/// and the framework) by identity. Sharing the contract assemblies is mandatory: if the plugin
/// loaded its own copy of <c>Opus.Engine.Consumer</c>, its <c>IConsumerIntegrationFactory</c>
/// would be a different <see cref="Type"/> than the host's and the reflection match would fail.
/// </summary>
/// <remarks>
/// The host-first resolution order also keeps a consumer that ships engine-named
/// assemblies working: a name the host cannot supply falls through to the plugin's own directory
/// (resolved from its <c>.deps.json</c> when present) and loads isolated here.
/// </remarks>
internal sealed class ConsumerPluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public ConsumerPluginLoadContext(string pluginAssemblyPath)
        : base(name: $"opus-consumer:{Path.GetFileNameWithoutExtension(pluginAssemblyPath)}", isCollectible: false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginAssemblyPath);
        _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Host first: if the default context can supply the dependency (every engine contract
        // assembly and the framework), share that instance so contract types have one identity.
        try
        {
            return Default.LoadFromAssemblyName(assemblyName);
        }
        catch (FileNotFoundException)
        {
            // Genuinely plugin-private (the host has no such assembly): load it isolated from
            // the plugin's own directory so the plugin's privates never leak into the host.
            var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);
            return resolvedPath is null ? null : LoadFromAssemblyPath(resolvedPath);
        }
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var resolvedPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return resolvedPath is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(resolvedPath);
    }
}
