using System;
using System.Collections.Generic;
using System.IO;

namespace Opus.Foundation;

/// <summary>
/// Applies a <see cref="RollingLogRetentionPolicy"/> to the existing files under a
/// rolling-log directory. The sweeper is a pure utility — the sink owns it transiently
/// at session open. Filesystem failures (permission denied, file in use) are swallowed
/// silently so a hostile environment cannot crash the host through log retention.
/// </summary>
internal static class RollingLogRetentionSweeper
{
    private const string LogExtension = ".log";

    /// <summary>Sweeps files matching <paramref name="filePrefix"/> + ".log" under
    /// <paramref name="directoryPath"/> and removes everything that exceeds either
    /// retention rule. <paramref name="now"/> is supplied by the caller so the sweep is
    /// deterministic in tests.</summary>
    public static void Sweep(
        string directoryPath,
        string filePrefix,
        RollingLogRetentionPolicy policy,
        DateTimeOffset now)
    {
        if (!policy.IsActive)
        {
            return;
        }

        List<FileInfo> candidates;
        try
        {
            candidates = EnumerateMatchingFiles(directoryPath, filePrefix);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        if (candidates.Count == 0)
        {
            return;
        }

        candidates.Sort(static (left, right) => right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc));
        ApplyAgeRule(candidates, policy, now);
        ApplyCountRule(candidates, policy);
    }

    private static List<FileInfo> EnumerateMatchingFiles(string directoryPath, string filePrefix)
    {
        var directory = new DirectoryInfo(directoryPath);
        if (!directory.Exists)
        {
            return new List<FileInfo>(0);
        }

        var pattern = filePrefix + "-*" + LogExtension;
        var enumeration = directory.EnumerateFiles(pattern, SearchOption.TopDirectoryOnly);
        var result = new List<FileInfo>();
        foreach (var file in enumeration)
        {
            result.Add(file);
        }

        return result;
    }

    private static void ApplyAgeRule(
        List<FileInfo> candidates,
        RollingLogRetentionPolicy policy,
        DateTimeOffset now)
    {
        if (policy.MaxAge <= TimeSpan.Zero)
        {
            return;
        }

        var ageThresholdUtc = (now - policy.MaxAge).UtcDateTime;
        for (var i = candidates.Count - 1; i >= 0; i--)
        {
            if (candidates[i].LastWriteTimeUtc <= ageThresholdUtc)
            {
                TryDelete(candidates[i]);
                candidates.RemoveAt(i);
            }
        }
    }

    private static void ApplyCountRule(List<FileInfo> candidates, RollingLogRetentionPolicy policy)
    {
        if (policy.MaxFileCount <= 0)
        {
            return;
        }

        // The sweep runs before the sink opens its own file, so we keep MaxFileCount - 1
        // existing files to make room for the new session log without immediately
        // breaching the limit.
        var allowed = Math.Max(0, policy.MaxFileCount - 1);
        for (var i = allowed; i < candidates.Count; i++)
        {
            TryDelete(candidates[i]);
        }
    }

    private static void TryDelete(FileInfo file)
    {
        try
        {
            file.Delete();
        }
        catch (IOException)
        {
            // File in use or locked; leave it for the next sweep.
        }
        catch (UnauthorizedAccessException)
        {
            // Permission denied; nothing useful we can do at session-open time.
        }
    }
}
