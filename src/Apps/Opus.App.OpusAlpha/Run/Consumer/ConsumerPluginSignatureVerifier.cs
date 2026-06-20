using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Opus.Foundation.Security;

namespace Opus.App.OpusAlpha.Run.Consumer;

internal static class ConsumerPluginSignatureVerifier
{
    private const string SupportedAlgorithm = "ecdsa-p256-sha256";
    private const long MaxAssemblyBytes = 128L * 1024 * 1024;
    private const long MaxSignatureBytes = 64L * 1024;
    private const long MaxPublicKeyBytes = 64L * 1024;

    public static ConsumerPluginTrustResult VerifyAndRead(
        string assemblyPath,
        string signaturePath,
        string publicKeyPath)
    {
        if (!File.Exists(signaturePath))
        {
            return ConsumerPluginTrustResult.Failure(
                $"Consumer signature '{signaturePath}' was not found.");
        }

        if (!File.Exists(publicKeyPath))
        {
            return ConsumerPluginTrustResult.Failure(
                $"Consumer trust key '{publicKeyPath}' was not found.");
        }

        try
        {
            var assemblyBytes = ReadBoundedBytes(assemblyPath, MaxAssemblyBytes);
            var envelope = JsonSerializer.Deserialize<ConsumerPluginSignature>(
                ReadBoundedBytes(signaturePath, MaxSignatureBytes));
            if (envelope is null
                || !string.Equals(envelope.Algorithm, SupportedAlgorithm, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(envelope.AssemblySha256)
                || string.IsNullOrWhiteSpace(envelope.Signature))
            {
                return ConsumerPluginTrustResult.Failure(
                    "Consumer signature envelope is malformed or uses an unsupported algorithm.");
            }

            using var key = ECDsa.Create();
            key.ImportFromPem(Encoding.UTF8.GetString(
                ReadBoundedBytes(publicKeyPath, MaxPublicKeyBytes)));
            if (!EcdsaKeyPolicy.IsNistP256(key))
            {
                return ConsumerPluginTrustResult.Failure(
                    "Consumer trust key must use the NIST P-256 curve.");
            }

            byte[] declaredHash;
            byte[] signature;
            try
            {
                declaredHash = Convert.FromHexString(envelope.AssemblySha256);
                signature = Convert.FromBase64String(envelope.Signature);
            }
            catch (FormatException)
            {
                return ConsumerPluginTrustResult.Failure(
                    "Consumer signature envelope contains invalid hash or signature encoding.");
            }

            var actualHash = SHA256.HashData(assemblyBytes);
            if (declaredHash.Length != actualHash.Length
                || !CryptographicOperations.FixedTimeEquals(actualHash, declaredHash)
                || !key.VerifyData(assemblyBytes, signature, HashAlgorithmName.SHA256))
            {
                return ConsumerPluginTrustResult.Failure(
                    "Consumer assembly signature verification failed.");
            }

            return ConsumerPluginTrustResult.SucceededWith(assemblyBytes);
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or CryptographicException
                                   or JsonException)
        {
            return ConsumerPluginTrustResult.Failure(
                $"Consumer signature verification failed: {ex.Message}");
        }
    }

    private static byte[] ReadBoundedBytes(string path, long maximumBytes)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.SequentialScan);
        if (stream.Length is <= 0 || stream.Length > maximumBytes)
        {
            throw new InvalidDataException(
                $"File '{path}' is empty or exceeds the {maximumBytes}-byte trust-boundary limit.");
        }

        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException(
                $"File '{path}' changed while it was being read.");
        }

        return bytes;
    }

    private sealed record ConsumerPluginSignature(
        string Algorithm,
        string AssemblySha256,
        string Signature);

    internal readonly record struct ConsumerPluginTrustResult(
        bool Succeeded,
        byte[]? AssemblyBytes,
        string? FailureReason)
    {
        public static ConsumerPluginTrustResult SucceededWith(byte[] assemblyBytes) =>
            new(true, assemblyBytes, null);

        public static ConsumerPluginTrustResult Failure(string reason) =>
            new(false, null, reason);
    }
}
