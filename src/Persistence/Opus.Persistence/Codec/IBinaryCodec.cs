using Opus.Foundation;

namespace Opus.Persistence;

/// <summary>
/// Round-trippable binary codec. The default impl in M1+ wraps MemoryPack; alternative
/// impls (zstd-compressed, signed-HMAC) plug in via DI without changing call sites.
/// </summary>
public interface IBinaryCodec
{
    byte[] Serialize<T>(T value);

    Result<T> Deserialize<T>(byte[] body);
}
