using System;

namespace Opus.Foundation;

/// <summary>Wall-clock abstraction. <see cref="GameTime"/> covers deterministic sim
/// time; this is for everything outside the sim — save timestamps, telemetry
/// envelopes, log envelopes, replay headers. Implementations live in the host
/// (<see cref="SystemClock"/> for production) and tests so unit specs can pin a
/// deterministic <see cref="long"/> instead of racing real time.</summary>
public interface IClock
{
    /// <summary>UTC time as Unix epoch milliseconds. Monotonicity is NOT promised —
    /// the OS clock may step. Callers that need monotonic readings use a
    /// <c>Stopwatch</c>-based clock instead.</summary>
    long UtcUnixMilliseconds();
}

/// <summary>Production <see cref="IClock"/> backed by <see cref="DateTimeOffset.UtcNow"/>.
/// Single shared instance is fine — the cmdlet has no state.</summary>
public sealed class SystemClock : IClock
{
    public long UtcUnixMilliseconds() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
