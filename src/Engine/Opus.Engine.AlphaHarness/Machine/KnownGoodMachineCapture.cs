using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Opus.Engine.AlphaHarness.Machine;

/// <summary>
/// Captures the engine-visible machine profile by reading <see cref="RuntimeInformation"/>
/// and <see cref="Environment"/>. The host supplies a graphics adapter name when a live
/// D3D12 device is available; otherwise null is recorded so a CI host can still capture a
/// profile without faking GPU data.
/// </summary>
public static class KnownGoodMachineCapture
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Captures a profile from the current process.</summary>
    /// <param name="profileName">Stable display name to record into the profile.</param>
    /// <param name="capturedAtUtc">UTC timestamp the caller wants stamped onto the
    /// profile. Tests usually pass a fixed timestamp; runtime hosts use
    /// <see cref="DateTimeOffset.UtcNow"/>.</param>
    /// <param name="graphicsAdapterName">Live D3D12 adapter name when available, or null
    /// when the host could not open a graphics device.</param>
    /// <param name="notes">Optional operator notes attached when authoring a known-good
    /// reference. Null on a fresh capture.</param>
    public static KnownGoodMachineProfile Capture(
        string profileName,
        DateTimeOffset capturedAtUtc,
        string? graphicsAdapterName,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("profileName must not be empty.", nameof(profileName));
        }

        return new KnownGoodMachineProfile(
            ProfileName: profileName,
            OperatingSystemFamily: DetectFamily(),
            OperatingSystemDescription: RuntimeInformation.OSDescription,
            ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString(),
            LogicalProcessorCount: Environment.ProcessorCount,
            DotnetRuntimeVersion: RuntimeInformation.FrameworkDescription,
            GraphicsAdapterName: NormaliseAdapterName(graphicsAdapterName),
            CapturedAtUtc: capturedAtUtc.ToUniversalTime(),
            Notes: NormaliseNotes(notes));
    }

    /// <summary>Serialises a profile as deterministic indented JSON. Used by the
    /// scripted capture command and the M9 known-good profile fixture.</summary>
    public static string Serialise(KnownGoodMachineProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        return JsonSerializer.Serialize(profile, JsonOptions);
    }

    /// <summary>Parses a profile JSON document. Returns null when the JSON is malformed;
    /// callers branch on null instead of catching exceptions because the file path is a
    /// boundary input.</summary>
    public static KnownGoodMachineProfile? TryParse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            var profile = JsonSerializer.Deserialize<KnownGoodMachineProfile>(json, JsonOptions);
            if (profile is null)
            {
                return null;
            }

            profile.Validate();
            return profile;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Reads a profile JSON from <paramref name="path"/>. Returns null when the
    /// file cannot be opened or parsed.</summary>
    public static KnownGoodMachineProfile? TryLoad(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return TryParse(File.ReadAllText(path));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static MachineOperatingSystemFamily DetectFamily()
    {
        if (OperatingSystem.IsWindows())
        {
            return MachineOperatingSystemFamily.Windows;
        }

        if (OperatingSystem.IsLinux())
        {
            return MachineOperatingSystemFamily.Linux;
        }

        if (OperatingSystem.IsMacOS())
        {
            return MachineOperatingSystemFamily.MacOs;
        }

        return MachineOperatingSystemFamily.Unknown;
    }

    private static string? NormaliseAdapterName(string? raw)
    {
        if (raw is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }

    private static string? NormaliseNotes(string? raw)
    {
        if (raw is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }
}
