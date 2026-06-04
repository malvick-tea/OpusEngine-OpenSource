namespace Opus.Engine.Pal.Hardware;

/// <summary>
/// Aggregate read-only view of the host hardware. Concrete probes live in the
/// platform-specific Pal assemblies; consumers only see this composed surface.
/// </summary>
public interface IHardwareInfo
{
    ICpuInfo Cpu { get; }

    IGpuInfo Gpu { get; }

    IRamInfo Ram { get; }

    IStorageInfo Storage { get; }
}

public interface ICpuInfo
{
    string Vendor { get; }

    string Model { get; }

    int LogicalCoreCount { get; }

    int PhysicalCoreCount { get; }

    /// <summary>Highest reported P-state frequency in MHz, or 0 if unknown.</summary>
    int MaxFrequencyMHz { get; }
}

public interface IGpuInfo
{
    string Vendor { get; }

    string Model { get; }

    long DedicatedVramBytes { get; }

    string DriverVersion { get; }
}

public interface IRamInfo
{
    long TotalBytes { get; }

    long AvailableBytes { get; }
}

public interface IStorageInfo
{
    string DataRootPath { get; }

    long FreeBytes { get; }

    long TotalBytes { get; }
}
