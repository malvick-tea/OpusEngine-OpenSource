using System;
using System.Collections.Generic;
using System.IO;

namespace Opus.Engine.Diagnostics.Reports;

/// <summary>
/// Applies a <see cref="DiagnosticsArtifactRetentionPolicy"/> to the JSON + text (and an
/// optional attached screenshot) diagnostics artifacts under a writer's output directory.
/// Treats the files sharing a stem as one retention unit so a sweep never leaves orphaned
/// members behind — when a report is retired its attached screenshot is retired with it.
/// A stem carrying only a screenshot and no report half is left untouched (a foreign file
/// is never the sweeper's to delete). Filesystem failures (permission denied, file in use)
/// are swallowed silently so a hostile environment cannot crash a tester run through
/// retention housekeeping.
/// </summary>
public static class DiagnosticsArtifactRetentionSweeper
{
    private const string JsonExtension = ".json";
    private const string TextExtension = ".txt";
    private const string ScreenshotExtension = ".png";

    private enum ArtifactKind
    {
        None,
        Json,
        Text,
        Screenshot,
    }

    /// <summary>Sweeps paired artifacts under <paramref name="directoryPath"/> whose
    /// stems start with <paramref name="stemPrefix"/> and removes everything that
    /// exceeds either retention rule. <paramref name="now"/> is caller-supplied so the
    /// sweep is deterministic in tests.</summary>
    public static void Sweep(
        string directoryPath,
        string stemPrefix,
        DiagnosticsArtifactRetentionPolicy policy,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(stemPrefix);
        ArgumentNullException.ThrowIfNull(policy);

        if (!policy.IsActive)
        {
            return;
        }

        policy.Validate();

        List<ArtifactPair> pairs;
        try
        {
            pairs = EnumeratePairs(directoryPath, stemPrefix);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        if (pairs.Count == 0)
        {
            return;
        }

        pairs.Sort(static (left, right) => right.NewestMtimeUtc.CompareTo(left.NewestMtimeUtc));
        ApplyAgeRule(pairs, policy, now);
        ApplyCountRule(pairs, policy);
    }

    private static List<ArtifactPair> EnumeratePairs(string directoryPath, string stemPrefix)
    {
        var directory = new DirectoryInfo(directoryPath);
        if (!directory.Exists)
        {
            return new List<ArtifactPair>(0);
        }

        var byStem = new Dictionary<string, ArtifactPairBuilder>(StringComparer.OrdinalIgnoreCase);
        var enumeration = directory.EnumerateFiles(
            stemPrefix + "*",
            SearchOption.TopDirectoryOnly);
        foreach (var file in enumeration)
        {
            var kind = Classify(file, out var stem);
            if (kind == ArtifactKind.None)
            {
                continue;
            }

            if (!byStem.TryGetValue(stem, out var builder))
            {
                builder = new ArtifactPairBuilder(stem);
                byStem[stem] = builder;
            }

            builder.Accept(file, kind);
        }

        var result = new List<ArtifactPair>(byStem.Count);
        foreach (var builder in byStem.Values)
        {
            // A stem with only a screenshot and no report half is not ours to retire; the
            // count/age rules operate on report units (a JSON or text member present).
            if (builder.HasReportMember)
            {
                result.Add(builder.Build());
            }
        }

        return result;
    }

    private static ArtifactKind Classify(FileInfo file, out string stem)
    {
        stem = string.Empty;
        if (string.Equals(file.Extension, JsonExtension, StringComparison.OrdinalIgnoreCase))
        {
            stem = Path.GetFileNameWithoutExtension(file.Name);
            return ArtifactKind.Json;
        }

        if (string.Equals(file.Extension, TextExtension, StringComparison.OrdinalIgnoreCase))
        {
            stem = Path.GetFileNameWithoutExtension(file.Name);
            return ArtifactKind.Text;
        }

        if (string.Equals(file.Extension, ScreenshotExtension, StringComparison.OrdinalIgnoreCase))
        {
            stem = Path.GetFileNameWithoutExtension(file.Name);
            return ArtifactKind.Screenshot;
        }

        return ArtifactKind.None;
    }

    private static void ApplyAgeRule(
        List<ArtifactPair> pairs,
        DiagnosticsArtifactRetentionPolicy policy,
        DateTimeOffset now)
    {
        if (policy.MaxAge <= TimeSpan.Zero)
        {
            return;
        }

        var ageThresholdUtc = (now - policy.MaxAge).UtcDateTime;
        for (var i = pairs.Count - 1; i >= 0; i--)
        {
            if (pairs[i].NewestMtimeUtc <= ageThresholdUtc)
            {
                pairs[i].DeleteAll();
                pairs.RemoveAt(i);
            }
        }
    }

    private static void ApplyCountRule(
        List<ArtifactPair> pairs,
        DiagnosticsArtifactRetentionPolicy policy)
    {
        if (policy.MaxPairCount <= 0)
        {
            return;
        }

        // The sweep runs before the next pair lands, so we keep MaxPairCount - 1 pairs
        // to make room for the new pair without immediately breaching the limit.
        var allowed = Math.Max(0, policy.MaxPairCount - 1);
        for (var i = allowed; i < pairs.Count; i++)
        {
            pairs[i].DeleteAll();
        }
    }

    private sealed class ArtifactPairBuilder
    {
        private readonly string _stem;
        private FileInfo? _json;
        private FileInfo? _text;
        private FileInfo? _screenshot;

        public ArtifactPairBuilder(string stem)
        {
            _stem = stem;
        }

        public bool HasReportMember => _json is not null || _text is not null;

        public void Accept(FileInfo file, ArtifactKind kind)
        {
            switch (kind)
            {
                case ArtifactKind.Json:
                    _json = file;
                    break;
                case ArtifactKind.Text:
                    _text = file;
                    break;
                case ArtifactKind.Screenshot:
                    _screenshot = file;
                    break;
            }
        }

        public ArtifactPair Build()
        {
            var newest = DateTime.MinValue;
            newest = Newer(newest, _json);
            newest = Newer(newest, _text);
            newest = Newer(newest, _screenshot);
            return new ArtifactPair(_stem, _json, _text, _screenshot, newest);
        }

        private static DateTime Newer(DateTime current, FileInfo? file) =>
            file is not null && file.LastWriteTimeUtc > current ? file.LastWriteTimeUtc : current;
    }

    private readonly struct ArtifactPair
    {
        public ArtifactPair(string stem, FileInfo? json, FileInfo? text, FileInfo? screenshot, DateTime newestMtimeUtc)
        {
            Stem = stem;
            Json = json;
            Text = text;
            Screenshot = screenshot;
            NewestMtimeUtc = newestMtimeUtc;
        }

        public string Stem { get; }

        public FileInfo? Json { get; }

        public FileInfo? Text { get; }

        public FileInfo? Screenshot { get; }

        public DateTime NewestMtimeUtc { get; }

        public void DeleteAll()
        {
            TryDelete(Json);
            TryDelete(Text);
            TryDelete(Screenshot);
        }

        private static void TryDelete(FileInfo? file)
        {
            if (file is null)
            {
                return;
            }

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
                // Permission denied; nothing useful we can do at write time.
            }
        }
    }
}
