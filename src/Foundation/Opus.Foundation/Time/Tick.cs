using System;

namespace Opus.Foundation;

/// <summary>
/// Strongly-typed simulation tick. 0-indexed monotonic counter.
/// Wraps <see cref="long"/> so we never confuse ticks with frames or seconds.
/// </summary>
public readonly record struct Tick(long Value) : IComparable<Tick>
{
    public static Tick Zero => new(0);

    public static Tick operator +(Tick a, int delta) => new(a.Value + delta);

    public static Tick operator -(Tick a, int delta) => new(a.Value - delta);

    public static long operator -(Tick a, Tick b) => a.Value - b.Value;

    public static bool operator <(Tick a, Tick b) => a.Value < b.Value;

    public static bool operator >(Tick a, Tick b) => a.Value > b.Value;

    public static bool operator <=(Tick a, Tick b) => a.Value <= b.Value;

    public static bool operator >=(Tick a, Tick b) => a.Value >= b.Value;

    public int CompareTo(Tick other) => Value.CompareTo(other.Value);

    public override string ToString() => $"t#{Value}";
}
