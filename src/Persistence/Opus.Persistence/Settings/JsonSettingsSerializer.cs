using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Opus.Foundation;

namespace Opus.Persistence.Settings;

/// <summary>
/// Serialises a settings record to / from human-editable JSON inside a versioned
/// <see cref="SettingsDocument{T}"/> envelope. Settings files are small, hand-inspectable, and
/// tester-editable, so the format is indented camel-case JSON with enums written as names — unlike
/// the binary save frame (<see cref="SaveHeaderSerializer"/>) which is codec-encoded for size.
/// <para>
/// Pure: this type does no file IO (that is the host's job per the module contract). The boundary
/// it guards is a deserialise: malformed JSON, an empty document, or a schema-version mismatch all
/// return a typed <see cref="ErrorCode.SettingsCorrupt"/> <see cref="Result{T}"/> rather than
/// throwing, so a caller falls back to defaults on a foreign or out-of-date file.
/// </para>
/// </summary>
public static class JsonSettingsSerializer
{
    /// <summary>Lowest accepted schema version. Versions are 1-based; 0 is reserved for "unset".</summary>
    public const int MinimumSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Serialises <paramref name="settings"/> into an indented JSON document tagged with
    /// <paramref name="schemaVersion"/>.</summary>
    public static string Serialize<T>(T settings, int schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (schemaVersion < MinimumSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion), schemaVersion, $"Schema version must be >= {MinimumSchemaVersion}.");
        }

        return JsonSerializer.Serialize(new SettingsDocument<T>(schemaVersion, settings), Options);
    }

    /// <summary>Parses <paramref name="json"/> and validates it against
    /// <paramref name="expectedSchemaVersion"/>. Returns the payload on success, or a typed
    /// <see cref="ErrorCode.SettingsCorrupt"/> error when the JSON is malformed, empty, or carries a
    /// different schema version.</summary>
    public static Result<T> Deserialize<T>(string json, int expectedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (expectedSchemaVersion < MinimumSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedSchemaVersion), expectedSchemaVersion, $"Schema version must be >= {MinimumSchemaVersion}.");
        }

        SettingsDocument<T>? document;
        try
        {
            document = JsonSerializer.Deserialize<SettingsDocument<T>>(json, Options);
        }
        catch (JsonException ex)
        {
            return Result<T>.Err(new Error(ErrorCode.SettingsCorrupt, "Settings JSON is malformed.", ex));
        }

        if (document is null || document.Settings is null)
        {
            return Result<T>.Err(ErrorCode.SettingsCorrupt, "Settings document is empty.");
        }

        if (document.SchemaVersion != expectedSchemaVersion)
        {
            return Result<T>.Err(
                ErrorCode.SettingsCorrupt,
                $"Settings schema version {document.SchemaVersion} does not match expected {expectedSchemaVersion}.");
        }

        return Result<T>.Ok(document.Settings);
    }
}
