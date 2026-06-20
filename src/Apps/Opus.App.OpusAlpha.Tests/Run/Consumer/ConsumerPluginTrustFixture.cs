using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Opus.App.OpusAlpha.Tests.Run.Consumer;

internal sealed class ConsumerPluginTrustFixture : IDisposable
{
    private ConsumerPluginTrustFixture(string root, string assemblyPath, string publicKeyPath)
    {
        Root = root;
        AssemblyPath = assemblyPath;
        PublicKeyPath = publicKeyPath;
    }

    public string Root { get; }

    public string AssemblyPath { get; }

    public string PublicKeyPath { get; }

    public static ConsumerPluginTrustFixture Create(string sourceAssemblyPath)
    {
        var root = Directory.CreateTempSubdirectory("opus-consumer-trust-").FullName;
        var assemblyPath = Path.Combine(root, Path.GetFileName(sourceAssemblyPath));
        File.Copy(sourceAssemblyPath, assemblyPath);

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKeyPath = Path.Combine(root, "consumer.pub.pem");
        File.WriteAllText(publicKeyPath, key.ExportSubjectPublicKeyInfoPem());

        var assemblyBytes = File.ReadAllBytes(assemblyPath);
        var envelope = new
        {
            Algorithm = "ecdsa-p256-sha256",
            AssemblySha256 = Convert.ToHexString(SHA256.HashData(assemblyBytes)).ToLowerInvariant(),
            Signature = Convert.ToBase64String(
                key.SignData(assemblyBytes, HashAlgorithmName.SHA256)),
        };
        File.WriteAllText(
            assemblyPath + ".sig",
            JsonSerializer.Serialize(envelope));
        return new ConsumerPluginTrustFixture(root, assemblyPath, publicKeyPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
