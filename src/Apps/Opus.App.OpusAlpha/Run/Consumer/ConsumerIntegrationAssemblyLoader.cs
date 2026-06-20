using System;
using System.Collections.Generic;
using System.IO;
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
    public static ConsumerIntegrationLoadResult Load(
        string assemblyPath,
        string trustedPublicKeyPath,
        string? signaturePath = null)
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

        if (string.IsNullOrWhiteSpace(trustedPublicKeyPath))
        {
            return ConsumerIntegrationLoadResult.Failure(
                "No trusted consumer public key was supplied.");
        }

        var trustResult = ConsumerPluginSignatureVerifier.VerifyAndRead(
            fullPath,
            signaturePath ?? fullPath + ".sig",
            trustedPublicKeyPath);
        if (!trustResult.Succeeded)
        {
            return ConsumerIntegrationLoadResult.Failure(
                trustResult.FailureReason ?? "Consumer signature verification failed.");
        }

        Assembly assembly;
        ConsumerPluginLoadContext? context = null;
        try
        {
            context = new ConsumerPluginLoadContext(fullPath);
            using var assemblyStream = new MemoryStream(
                trustResult.AssemblyBytes
                    ?? throw new InvalidOperationException("Verified consumer bytes are unavailable."),
                writable: false);
            assembly = context.LoadFromStream(assemblyStream);
        }
        catch (Exception ex) when (ex is BadImageFormatException
                                   or FileLoadException
                                   or InvalidOperationException
                                   or DllNotFoundException)
        {
            context?.Unload();
            return ConsumerIntegrationLoadResult.Failure(
                $"Consumer assembly '{fullPath}' is not a loadable managed assembly: {ex.Message}");
        }

        IReadOnlyList<Type> types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            context.Unload();
            return ConsumerIntegrationLoadResult.Failure(
                $"Consumer assembly '{fullPath}' has unresolved type dependencies: {ex.Message}");
        }

        var result = ConsumerIntegrationFactoryResolver.Resolve(types, Path.GetFileName(fullPath));
        if (!result.Succeeded)
        {
            context.Unload();
            return result;
        }

        return result.AttachLifetime(new ConsumerPluginLifetime(context));
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
}
