using System;
using MemoryPack;
using Opus.Foundation;

namespace Opus.Persistence;

/// <summary>
/// Default <see cref="IBinaryCodec"/> over MemoryPack. Thin wrapper — MemoryPack is
/// source-generated and AOT-friendly, exactly what we need for the iOS build.
/// </summary>
public sealed class MemoryPackCodec : IBinaryCodec
{
    public byte[] Serialize<T>(T value) => MemoryPackSerializer.Serialize(value);

    public Result<T> Deserialize<T>(byte[] body)
    {
        try
        {
            var value = MemoryPackSerializer.Deserialize<T>(body);
            return value is null
                ? Result<T>.Err(ErrorCode.SaveCorrupt, $"Deserialize<{typeof(T).Name}> returned null.")
                : Result<T>.Ok(value);
        }
        catch (Exception ex)
        {
            return Result<T>.Err(new Error(
                ErrorCode.SaveCorrupt, $"MemoryPack deserialize failed for {typeof(T).Name}", ex));
        }
    }
}
