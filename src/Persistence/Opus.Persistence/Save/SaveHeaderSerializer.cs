using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Opus.Foundation;

namespace Opus.Persistence;

/// <summary>Frames a <see cref="SaveHeader"/> + codec-encoded body into a single byte
/// stream and decodes it back. Magic + schema-version are inspected BEFORE the body is
/// fed to <see cref="IBinaryCodec.Deserialize{T}"/>, so a foreign or out-of-date file is
/// rejected as <see cref="ErrorCode.SaveCorrupt"/> without burning a MemoryPack
/// deserialize cycle.</summary>
/// <remarks>
/// Frame layout (little-endian):
/// <list type="number">
/// <item><description>Length-prefixed magic string (4 bytes len + UTF-8 bytes).</description></item>
/// <item><description>Schema version (int32).</description></item>
/// <item><description><see cref="AppVersion"/> components: major/minor/patch int32s + two
/// length-prefixed strings (pre-release, build).</description></item>
/// <item><description>Created-at Unix ms (int64).</description></item>
/// <item><description>Body length (int32) + raw body bytes.</description></item>
/// </list>
/// Hand-rolled rather than MemoryPack-attributed so neither <see cref="SaveHeader"/>
/// nor <see cref="AppVersion"/> needs to carry codec metadata — Foundation stays
/// codec-free, Persistence owns the framing.
/// </remarks>
public static class SaveHeaderSerializer
{
    public const int AuthenticationTagBytes = 32;
    public const int MinimumAuthenticationKeyBytes = 32;
    public const int MaxFrameBytes = 64 * 1024 * 1024;

    /// <summary>Default HKDF-Expand info label used when a caller does not
    /// supply an explicit domain. Keeping the legacy path means old tests and
    /// any external tooling that calls <see cref="WriteFrame{T}"/> directly
    /// continue to round-trip; new callers should pass a per-blob domain so
    /// the same install key cannot be replayed across blob types.</summary>
    public static readonly byte[] DefaultDomain = Encoding.UTF8.GetBytes("Opus.Save.v1");

    public static byte[] WriteFrame<T>(
        SaveHeader header,
        T body,
        IBinaryCodec codec,
        ReadOnlySpan<byte> authenticationKey)
        => WriteFrame(header, body, codec, authenticationKey, DefaultDomain);

    /// <summary>Serialises <paramref name="header"/> + <paramref name="body"/>
    /// and MACs the bytes with an HKDF-Expand sub-key derived from
    /// <paramref name="authenticationKey"/> scoped to <paramref name="domain"/>.
    /// Domain separation prevents the same install key from validating two
    /// different blob types — a tampered settings blob cannot be replayed as a
    /// progress blob, and vice versa, because the derived MAC keys are
    /// independent even though the wrapping key is identical.</summary>
    public static byte[] WriteFrame<T>(
        SaveHeader header,
        T body,
        IBinaryCodec codec,
        ReadOnlySpan<byte> authenticationKey,
        ReadOnlySpan<byte> domain)
    {
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentException.ThrowIfNullOrEmpty(header.Magic);
        ValidateAuthenticationKey(authenticationKey);
        if (domain.IsEmpty)
        {
            throw new ArgumentException("Save frame domain must not be empty.", nameof(domain));
        }

        var bodyBytes = codec.Serialize(body);
        if (bodyBytes.Length > MaxFrameBytes - AuthenticationTagBytes)
        {
            throw new InvalidDataException(
                $"Serialized save body exceeds the {MaxFrameBytes}-byte frame limit.");
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteLengthPrefixedString(writer, header.Magic);
        WriteInt32(writer, header.SchemaVersion);
        WriteAppVersion(writer, header.AuthoringVersion);
        WriteInt64(writer, header.CreatedAtUnixMs);
        WriteLengthPrefixedBytes(writer, bodyBytes);
        writer.Flush();
        if (stream.Length > MaxFrameBytes - AuthenticationTagBytes)
        {
            throw new InvalidDataException(
                $"Save frame exceeds the {MaxFrameBytes}-byte limit.");
        }

        var authenticatedBytes = stream.ToArray();
        Span<byte> macKey = stackalloc byte[AuthenticationTagBytes];
        HKDF.Expand(HashAlgorithmName.SHA256, authenticationKey, macKey.Length, domain, macKey);
        var output = new byte[authenticatedBytes.Length + AuthenticationTagBytes];
        authenticatedBytes.CopyTo(output, 0);
        HMACSHA256.HashData(
            macKey,
            authenticatedBytes,
            output.AsSpan(authenticatedBytes.Length));
        CryptographicOperations.ZeroMemory(macKey);
        return output;
    }

    public static Result<(SaveHeader Header, T Body)> ReadFrame<T>(
        byte[] frame,
        IBinaryCodec codec,
        ReadOnlySpan<byte> authenticationKey)
        => ReadFrame(frame, codec, authenticationKey, DefaultDomain);

    /// <summary>Counterpart to <see cref="WriteFrame{T}(SaveHeader, T, IBinaryCodec, ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>.
    /// Derives the same HKDF sub-key scoped to <paramref name="domain"/> and
    /// verifies the trailing HMAC-SHA256 tag in constant time before any of
    /// the frame bytes are parsed.</summary>
    public static Result<(SaveHeader Header, T Body)> ReadFrame<T>(
        byte[] frame,
        IBinaryCodec codec,
        ReadOnlySpan<byte> authenticationKey,
        ReadOnlySpan<byte> domain)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(codec);
        ValidateAuthenticationKey(authenticationKey);
        if (domain.IsEmpty)
        {
            return Result<(SaveHeader, T)>.Err(
                ErrorCode.SaveCorrupt,
                "Save frame domain must not be empty.");
        }

        if (frame.Length < AuthenticationTagBytes || frame.Length > MaxFrameBytes)
        {
            return Result<(SaveHeader, T)>.Err(
                ErrorCode.SaveCorrupt,
                "Save frame length is outside the authenticated format limits.");
        }

        var authenticatedLength = frame.Length - AuthenticationTagBytes;
        var authenticatedBytes = frame.AsSpan(0, authenticatedLength);
        Span<byte> macKey = stackalloc byte[AuthenticationTagBytes];
        HKDF.Expand(HashAlgorithmName.SHA256, authenticationKey, macKey.Length, domain, macKey);
        Span<byte> expectedTag = stackalloc byte[AuthenticationTagBytes];
        HMACSHA256.HashData(macKey, authenticatedBytes, expectedTag);
        CryptographicOperations.ZeroMemory(macKey);
        if (!CryptographicOperations.FixedTimeEquals(
                expectedTag,
                frame.AsSpan(authenticatedLength, AuthenticationTagBytes)))
        {
            return Result<(SaveHeader, T)>.Err(
                ErrorCode.SaveCorrupt,
                "Save frame authentication failed.");
        }

        try
        {
            using var stream = new MemoryStream(
                frame,
                index: 0,
                count: authenticatedLength,
                writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var magic = ReadLengthPrefixedString(reader);
            var schemaVersion = ReadInt32(reader);
            var appVersion = ReadAppVersion(reader);
            var createdAt = ReadInt64(reader);
            var bodyBytes = ReadLengthPrefixedBytes(reader);
            if (stream.Position != stream.Length)
            {
                return Result<(SaveHeader, T)>.Err(
                    ErrorCode.SaveCorrupt,
                    "Save frame contains trailing structure.");
            }

            var header = new SaveHeader(magic, schemaVersion, appVersion, createdAt);
            if (!header.IsRecognisedMagic)
            {
                return Result<(SaveHeader, T)>.Err(
                    ErrorCode.SaveCorrupt, $"Unrecognised save magic \"{magic}\" (expected {SaveHeader.MagicV1}).");
            }

            var bodyResult = codec.Deserialize<T>(bodyBytes);
            return bodyResult.IsErr
                ? Result<(SaveHeader, T)>.Err(bodyResult.UnwrapErr())
                : Result<(SaveHeader, T)>.Ok((header, bodyResult.Unwrap()));
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException or ArgumentException)
        {
            return Result<(SaveHeader, T)>.Err(new Error(
                ErrorCode.SaveCorrupt, "Save frame is structurally malformed.", ex));
        }
    }

    private static void ValidateAuthenticationKey(ReadOnlySpan<byte> authenticationKey)
    {
        if (authenticationKey.Length < MinimumAuthenticationKeyBytes)
        {
            throw new ArgumentException(
                $"Save authentication key must contain at least {MinimumAuthenticationKeyBytes} bytes.",
                nameof(authenticationKey));
        }
    }

    private static void WriteInt32(BinaryWriter writer, int value)
    {
        Span<byte> buf = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(buf, value);
        writer.Write(buf);
    }

    private static void WriteInt64(BinaryWriter writer, long value)
    {
        Span<byte> buf = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(buf, value);
        writer.Write(buf);
    }

    private static void WriteAppVersion(BinaryWriter writer, AppVersion version)
    {
        WriteInt32(writer, version.Major);
        WriteInt32(writer, version.Minor);
        WriteInt32(writer, version.Patch);
        WriteLengthPrefixedString(writer, version.PreRelease ?? string.Empty);
        WriteLengthPrefixedString(writer, version.Build ?? string.Empty);
    }

    private static void WriteLengthPrefixedString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteLengthPrefixedBytes(writer, bytes);
    }

    private static void WriteLengthPrefixedBytes(BinaryWriter writer, byte[] payload)
    {
        WriteInt32(writer, payload.Length);
        writer.Write(payload);
    }

    private static int ReadInt32(BinaryReader reader)
    {
        Span<byte> buf = stackalloc byte[sizeof(int)];
        ReadExactly(reader, buf);
        return BinaryPrimitives.ReadInt32LittleEndian(buf);
    }

    private static long ReadInt64(BinaryReader reader)
    {
        Span<byte> buf = stackalloc byte[sizeof(long)];
        ReadExactly(reader, buf);
        return BinaryPrimitives.ReadInt64LittleEndian(buf);
    }

    private static AppVersion ReadAppVersion(BinaryReader reader)
    {
        var major = ReadInt32(reader);
        var minor = ReadInt32(reader);
        var patch = ReadInt32(reader);
        var pre = ReadLengthPrefixedString(reader);
        var build = ReadLengthPrefixedString(reader);
        return new AppVersion(major, minor, patch, pre, build);
    }

    private static string ReadLengthPrefixedString(BinaryReader reader)
    {
        var bytes = ReadLengthPrefixedBytes(reader);
        return Encoding.UTF8.GetString(bytes);
    }

    private static byte[] ReadLengthPrefixedBytes(BinaryReader reader)
    {
        var len = ReadInt32(reader);
        if (len < 0)
        {
            throw new IOException($"Negative length prefix: {len}.");
        }

        // Bound the prefix against the bytes actually present before allocating. The frame is
        // untrusted (a save / replay file on disk), and a hostile or corrupt prefix can claim up to
        // ~2 GiB that BinaryReader.ReadBytes would pre-allocate in full before discovering the data
        // is not there — an OOM on load. Reading the seekable frame's remaining length caps the
        // allocation at the real frame size.
        var remaining = reader.BaseStream.Length - reader.BaseStream.Position;
        if (len > remaining)
        {
            throw new EndOfStreamException(
                $"Length prefix {len} exceeds the {remaining} bytes remaining in the frame.");
        }

        var payload = reader.ReadBytes(len);
        if (payload.Length != len)
        {
            throw new EndOfStreamException(
                $"Expected {len} bytes for length-prefixed segment, got {payload.Length}.");
        }

        return payload;
    }

    private static void ReadExactly(BinaryReader reader, Span<byte> destination)
    {
        var read = reader.Read(destination);
        if (read != destination.Length)
        {
            throw new EndOfStreamException(
                $"Expected {destination.Length} bytes, got {read}.");
        }
    }
}
