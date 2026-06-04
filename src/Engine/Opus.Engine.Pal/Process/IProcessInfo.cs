namespace Opus.Engine.Pal.Process;

/// <summary>
/// Snapshot of the current OS process. Read on demand by diagnostics and crash handlers.
/// Implementations populate as much as the platform exposes; fields the OS hides return
/// sensible defaults (-1 for IDs, empty string for paths) rather than throwing.
/// </summary>
public interface IProcessInfo
{
    int ProcessId { get; }

    string ExecutablePath { get; }

    string CommandLine { get; }

    long PrivateBytes { get; }

    long WorkingSetBytes { get; }

    System.TimeSpan UpTime { get; }
}
