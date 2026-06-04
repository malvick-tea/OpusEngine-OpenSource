using System;
using System.IO;
using Opus.Engine.Pal.Hardware;

namespace Opus.Engine.Pal.Windows.Hardware;

/// <summary>
/// Conservative defaults using only managed primitives. Native probes (CPUID for Vendor,
/// DXGI for GPU enumeration, GlobalMemoryStatusEx for RAM, real driver versions) land
/// in M3 telemetry — they require P/Invoke and aren't on the hello-world critical path.
/// </summary>
public sealed class WindowsHardwareInfo : IHardwareInfo
{
    public WindowsHardwareInfo(string dataRootPath)
    {
        Cpu = new ManagedCpuInfo();
        Gpu = new UnknownGpuInfo();
        Ram = new ManagedRamInfo();
        Storage = new ManagedStorageInfo(dataRootPath);
    }

    public ICpuInfo Cpu { get; }

    public IGpuInfo Gpu { get; }

    public IRamInfo Ram { get; }

    public IStorageInfo Storage { get; }

    private sealed class ManagedCpuInfo : ICpuInfo
    {
        public string Vendor => "unknown";

        public string Model => Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown";

        public int LogicalCoreCount => Environment.ProcessorCount;

        public int PhysicalCoreCount => Environment.ProcessorCount;

        public int MaxFrequencyMHz => 0;
    }

    private sealed class UnknownGpuInfo : IGpuInfo
    {
        public string Vendor => "unknown";

        public string Model => "unknown";

        public long DedicatedVramBytes => 0;

        public string DriverVersion => string.Empty;
    }

    private sealed class ManagedRamInfo : IRamInfo
    {
        public long TotalBytes => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

        public long AvailableBytes
        {
            get
            {
                var info = GC.GetGCMemoryInfo();
                return Math.Max(0, info.TotalAvailableMemoryBytes - info.MemoryLoadBytes);
            }
        }
    }

    private sealed class ManagedStorageInfo : IStorageInfo
    {
        public ManagedStorageInfo(string dataRootPath)
        {
            DataRootPath = dataRootPath;
        }

        public string DataRootPath { get; }

        public long FreeBytes
        {
            get
            {
                try
                {
                    var drive = new DriveInfo(Path.GetPathRoot(DataRootPath) ?? "C:\\");
                    return drive.IsReady ? drive.AvailableFreeSpace : 0;
                }
                catch
                {
                    return 0;
                }
            }
        }

        public long TotalBytes
        {
            get
            {
                try
                {
                    var drive = new DriveInfo(Path.GetPathRoot(DataRootPath) ?? "C:\\");
                    return drive.IsReady ? drive.TotalSize : 0;
                }
                catch
                {
                    return 0;
                }
            }
        }
    }
}
