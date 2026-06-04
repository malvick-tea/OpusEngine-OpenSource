using System;
using System.Diagnostics;
using Opus.Engine.Pal.Process;

namespace Opus.Engine.Pal.Windows.Process;

public sealed class WindowsProcessInfo : IProcessInfo
{
    private readonly System.Diagnostics.Process _proc = System.Diagnostics.Process.GetCurrentProcess();
    private readonly DateTimeOffset _started = DateTimeOffset.UtcNow;

    public int ProcessId => _proc.Id;

    public string ExecutablePath => Environment.ProcessPath ?? string.Empty;

    public string CommandLine => Environment.CommandLine;

    public long PrivateBytes
    {
        get
        {
            _proc.Refresh();
            return _proc.PrivateMemorySize64;
        }
    }

    public long WorkingSetBytes
    {
        get
        {
            _proc.Refresh();
            return _proc.WorkingSet64;
        }
    }

    public TimeSpan UpTime => DateTimeOffset.UtcNow - _started;
}
