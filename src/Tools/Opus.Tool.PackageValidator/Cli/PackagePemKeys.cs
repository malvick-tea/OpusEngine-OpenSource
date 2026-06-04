using System.Security.Cryptography;

namespace Opus.Tool.PackageValidator;

/// <summary>
/// Loads an ECDSA key from a PEM file at the CLI boundary. The engine ships no keys: the signer
/// and verifier operate on a caller-supplied key, so PEM loading lives in the tool rather than in
/// the packaging library.
/// </summary>
internal static class PackagePemKeys
{
    /// <summary>Loads an ECDSA key (public or private, depending on the PEM contents) from
    /// <paramref name="path"/>. Returns false with a human-readable <paramref name="error"/> when
    /// the file is missing or is not a usable EC key.</summary>
    public static bool TryLoad(string path, out ECDsa? key, out string? error)
    {
        key = null;
        error = null;
        if (!File.Exists(path))
        {
            error = $"Key file '{path}' does not exist.";
            return false;
        }

        string pem;
        try
        {
            pem = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            error = ex.Message;
            return false;
        }

        var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportFromPem(pem);
            key = ecdsa;
            return true;
        }
        catch (ArgumentException ex)
        {
            ecdsa.Dispose();
            error = ex.Message;
            return false;
        }
        catch (CryptographicException ex)
        {
            ecdsa.Dispose();
            error = ex.Message;
            return false;
        }
    }
}
