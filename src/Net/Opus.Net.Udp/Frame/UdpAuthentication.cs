using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Opus.Net.Transport;

namespace Opus.Net.Udp.Frame;

/// <summary>Cryptographic primitives for the UDP handshake and session framing.</summary>
public static class UdpAuthentication
{
    public const int MinimumKeyBytes = 32;
    public const int NonceBytes = 32;
    public const int SessionKeyBytes = 32;
    public const int DirectionKeyBytes = 32;
    private const int MaxKeyFileBytes = 4096;

    private static ReadOnlySpan<byte> PasswordSalt => "Opus.Net.Udp.Psk.v2"u8;
    private static ReadOnlySpan<byte> SessionContext => "Opus.Net.Udp.Session.v2"u8;
    private static ReadOnlySpan<byte> ClientToServerLabel => "Opus.Net.Udp.Direction.c2s.v1"u8;
    private static ReadOnlySpan<byte> ServerToClientLabel => "Opus.Net.Udp.Direction.s2c.v1"u8;

    /// <summary>Direction tag carried inside the HKDF info buffer so the two
    /// direction-specific keys cannot collide even if every other input is
    /// identical. Mirrors the label bytes above; surfaced as a typed enum so
    /// call sites stay readable.</summary>
    public enum Direction : byte
    {
        /// <summary>Key used by the client to MAC outbound frames and by the
        /// server to verify inbound frames. Never accepted on a server →
        /// client frame.</summary>
        ClientToServer = 1,

        /// <summary>Key used by the server to MAC outbound frames and by the
        /// client to verify inbound frames. Never accepted on a client →
        /// server frame.</summary>
        ServerToClient = 2,
    }

    /// <summary>Derives a transport key from an operator-provided secret. The salt is a public
    /// protocol domain separator; deployments must provide a high-entropy secret.</summary>
    public static byte[] DeriveKey(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        if (secret.Length < 16)
        {
            throw new ArgumentException(
                "UDP authentication secrets must contain at least 16 characters.",
                nameof(secret));
        }

        var passwordBytes = Encoding.UTF8.GetBytes(secret);
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                passwordBytes,
                PasswordSalt,
                iterations: 210_000,
                HashAlgorithmName.SHA256,
                outputLength: MinimumKeyBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public static byte[] ReadKeyFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fileBytes = ReadBoundedKeyFile(path);

        try
        {
            var material = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true)
                .GetString(fileBytes)
                .Trim();
            if (material.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
            {
                return ValidateRawKey(Convert.FromBase64String(material[7..]));
            }

            if (material.StartsWith("hex:", StringComparison.OrdinalIgnoreCase))
            {
                return ValidateRawKey(Convert.FromHexString(material[4..]));
            }

            return DeriveKey(material);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(fileBytes);
        }
    }

    private static byte[] ReadBoundedKeyFile(string path)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: MaxKeyFileBytes,
                FileOptions.SequentialScan);
        }
        catch (FileNotFoundException)
        {
            throw new FileNotFoundException(
                "UDP authentication key file was not found.",
                path);
        }

        using (stream)
        {
            if (stream.Length is <= 0 or > MaxKeyFileBytes)
            {
                throw new InvalidDataException(
                    $"UDP authentication key file must contain between 1 and {MaxKeyFileBytes} bytes.");
            }

            var bytes = new byte[checked((int)stream.Length)];
            try
            {
                stream.ReadExactly(bytes);
                if (stream.ReadByte() != -1)
                {
                    throw new InvalidDataException(
                        $"UDP authentication key file exceeds {MaxKeyFileBytes} bytes.");
                }

                return bytes;
            }
            catch
            {
                CryptographicOperations.ZeroMemory(bytes);
                throw;
            }
        }
    }

    public static byte[] CreateNonce() => RandomNumberGenerator.GetBytes(NonceBytes);

    public static ConnectionId CreateConnectionId(Func<ConnectionId, bool> isAvailable)
    {
        ArgumentNullException.ThrowIfNull(isAvailable);
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        while (true)
        {
            RandomNumberGenerator.Fill(bytes);
            var candidate = new ConnectionId(BinaryPrimitives.ReadUInt64LittleEndian(bytes));
            if (candidate.IsValid && isAvailable(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>Derives the shared session key from the PSK and the two
    /// handshake nonces. The session key is an intermediate value — callers
    /// must feed it through <see cref="DeriveDirectionKey"/> before using it
    /// to MAC frames in either direction.</summary>
    public static byte[] DeriveSessionKey(
        ReadOnlySpan<byte> preSharedKey,
        ReadOnlySpan<byte> clientNonce,
        ReadOnlySpan<byte> serverNonce,
        ConnectionId connectionId)
    {
        ValidateNonce(clientNonce, nameof(clientNonce));
        ValidateNonce(serverNonce, nameof(serverNonce));

        Span<byte> material = stackalloc byte[
            SessionContext.Length + (NonceBytes * 2) + sizeof(ulong)];
        var cursor = 0;
        SessionContext.CopyTo(material);
        cursor += SessionContext.Length;
        clientNonce.CopyTo(material[cursor..]);
        cursor += NonceBytes;
        serverNonce.CopyTo(material[cursor..]);
        cursor += NonceBytes;
        BinaryPrimitives.WriteUInt64LittleEndian(material[cursor..], connectionId.Value);
        return HMACSHA256.HashData(preSharedKey, material);
    }

    /// <summary>Derives a direction-specific MAC key from the shared session
    /// key using HKDF-Expand (RFC 5869). Two keys are produced per session —
    /// one for client → server frames, one for server → client frames — so a
    /// frame MAC'd in one direction never verifies when reflected back to the
    /// sender. The label is fixed per direction so both peers derive the same
    /// pair without an extra round-trip.</summary>
    /// <remarks>
    /// HKDF-Expand alone (no HKDF-Extract) is correct here because the session
    /// key is already a uniformly-random 32-byte value from
    /// <see cref="DeriveSessionKey"/>. Running Extract on it would just add a
    /// needless PRF pass. The info buffer carries the protocol label, the
    /// direction tag, and the connection id so two sessions on the same PSK
    /// cannot reuse a key across directions or across connections.
    /// </remarks>
    public static byte[] DeriveDirectionKey(
        ReadOnlySpan<byte> sessionKey,
        ConnectionId connectionId,
        Direction direction)
    {
        if (sessionKey.Length < SessionKeyBytes)
        {
            throw new ArgumentException(
                $"Session key must contain at least {SessionKeyBytes} bytes.",
                nameof(sessionKey));
        }

        var label = direction switch
        {
            Direction.ClientToServer => ClientToServerLabel,
            Direction.ServerToClient => ServerToClientLabel,
            _ => throw new ArgumentOutOfRangeException(
                nameof(direction),
                direction,
                "Unknown UDP session direction."),
        };

        // HKDF-Expand info = label || direction_byte || connection_id (little-endian).
        // 32 bytes is exactly HMAC-SHA256 output, so a single HKDF-Expand block.
        Span<byte> info = stackalloc byte[
            label.Length + sizeof(byte) + sizeof(ulong)];
        var cursor = 0;
        label.CopyTo(info);
        cursor += label.Length;
        info[cursor++] = (byte)direction;
        BinaryPrimitives.WriteUInt64LittleEndian(info[cursor..], connectionId.Value);

        var output = new byte[DirectionKeyBytes];
        HKDF.Expand(HashAlgorithmName.SHA256, sessionKey, output.Length, info, output);
        return output;
    }

    private static void ValidateNonce(ReadOnlySpan<byte> nonce, string name)
    {
        if (nonce.Length != NonceBytes)
        {
            throw new ArgumentException($"UDP handshake nonce must be {NonceBytes} bytes.", name);
        }
    }

    private static byte[] ValidateRawKey(byte[] key)
    {
        if (key.Length < MinimumKeyBytes)
        {
            CryptographicOperations.ZeroMemory(key);
            throw new InvalidDataException(
                $"Raw UDP authentication keys must contain at least {MinimumKeyBytes} bytes.");
        }

        return key;
    }
}
