using System.Security.Cryptography;

namespace Opus.Content.Packaging.Tests.Fixtures;

/// <summary>Helpers for ECDSA P-256 keys in tests, including PEM export for key-file paths.</summary>
internal static class EcdsaTestKeys
{
    public static ECDsa CreateP256() => ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public static string WritePrivatePem(ECDsa key, string path)
    {
        File.WriteAllText(path, key.ExportPkcs8PrivateKeyPem());
        return path;
    }

    public static string WritePublicPem(ECDsa key, string path)
    {
        File.WriteAllText(path, key.ExportSubjectPublicKeyInfoPem());
        return path;
    }
}
