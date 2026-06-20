using System.Security.Cryptography;

namespace Opus.Foundation.Security;

/// <summary>Named-curve policy shared by detached-signature trust boundaries.</summary>
public static class EcdsaKeyPolicy
{
    private const string NistP256ObjectIdentifier = "1.2.840.10045.3.1.7";

    public static bool IsNistP256(ECDsa key)
    {
        ArgumentNullException.ThrowIfNull(key);
        try
        {
            var parameters = key.ExportParameters(includePrivateParameters: false);
            return key.KeySize == 256
                && string.Equals(
                    parameters.Curve.Oid.Value,
                    NistP256ObjectIdentifier,
                    StringComparison.Ordinal);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
