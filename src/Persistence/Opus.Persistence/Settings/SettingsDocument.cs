namespace Opus.Persistence.Settings;

/// <summary>
/// Versioned envelope written around a settings payload so a schema change is detectable on load.
/// <see cref="JsonSettingsSerializer"/> wraps the payload in one of these before serialising and
/// gates on <see cref="SchemaVersion"/> when reading, so an out-of-date or foreign settings file is
/// rejected before the caller trusts a half-migrated shape. Genre-neutral and codec-free: the
/// payload <typeparamref name="T"/> is whatever settings record a consumer (engine host, tester
/// tool, game) wants to persist.
/// </summary>
/// <typeparam name="T">The settings payload record.</typeparam>
/// <param name="SchemaVersion">On-disk shape version of <paramref name="Settings"/>. Bump when the
/// payload's fields change so an older reader rejects the newer file instead of mis-parsing it.</param>
/// <param name="Settings">The settings payload.</param>
public sealed record SettingsDocument<T>(int SchemaVersion, T Settings);
