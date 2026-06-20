using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Opus.App.OpusAlpha.Run.Consumer;

/// <summary>
/// Loads a signed external consumer plugin while sharing only assemblies already trusted by the
/// host. Private managed and unmanaged dependencies are denied because the detached signature
/// authenticates exactly one assembly.
/// </summary>
internal sealed class ConsumerPluginLoadContext : AssemblyLoadContext
{
    public ConsumerPluginLoadContext(string pluginAssemblyPath)
        : base(name: $"opus-consumer:{Path.GetFileNameWithoutExtension(pluginAssemblyPath)}", isCollectible: true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginAssemblyPath);
        _ = Path.GetFullPath(pluginAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        try
        {
            return Default.LoadFromAssemblyName(assemblyName);
        }
        catch (FileNotFoundException ex)
        {
            throw new FileLoadException(
                $"Consumer plugin dependency '{assemblyName.FullName}' is not supplied by the trusted host.",
                ex);
        }
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        throw new DllNotFoundException(
            $"Consumer plugins may not load unmanaged library '{unmanagedDllName}'.");
    }
}
