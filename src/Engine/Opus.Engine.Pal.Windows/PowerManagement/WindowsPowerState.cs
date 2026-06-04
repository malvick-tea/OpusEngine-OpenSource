using System;
using Opus.Engine.Pal.PowerManagement;

namespace Opus.Engine.Pal.Windows.PowerManagement;

/// <summary>
/// Desktop Windows reports stable thermal state. Mobile Pal impls actually drive
/// this signal; on desktop we keep it nominal so quality presets don't downshift.
/// </summary>
public sealed class WindowsThermalState : IThermalState
{
    public ThermalLevel Level => ThermalLevel.Nominal;

    public event Action<ThermalLevel, ThermalLevel>? LevelChanged
    {
        add => _ = value;
        remove => _ = value;
    }
}

/// <summary>
/// Desktop default — assume AC. Real laptop/battery probing via SYSTEM_POWER_STATUS
/// (P/Invoke to GetSystemPowerStatus) lands when we ship for laptops as a separate SKU.
/// </summary>
public sealed class WindowsBatteryState : IBatteryState
{
    public BatterySource Source => BatterySource.AcAdapter;

    public float? Level => null;

    public event Action<BatterySource, float?>? Changed
    {
        add => _ = value;
        remove => _ = value;
    }
}
