using System;

namespace Opus.Engine.Pal.PowerManagement;

/// <summary>
/// Mobile-first power awareness. On desktop the values are stable and uninteresting;
/// on Android / iOS we throttle background simulation, lower target framerate, and
/// reduce shader quality when these signal stress.
/// </summary>
public interface IThermalState
{
    ThermalLevel Level { get; }

    event Action<ThermalLevel, ThermalLevel>? LevelChanged;
}

public interface IBatteryState
{
    BatterySource Source { get; }

    /// <summary>0..1, or null if the platform does not expose level.</summary>
    float? Level { get; }

    event Action<BatterySource, float?>? Changed;
}

public enum ThermalLevel
{
    Nominal,
    Fair,
    Serious,
    Critical,
}

public enum BatterySource
{
    Unknown,
    AcAdapter,
    Battery,
}

/// <summary>Coarse profile derived from thermal + battery state, used to bias quality knobs.</summary>
public enum PowerProfile
{
    HighPerformance,
    Balanced,
    PowerSaver,
}
