namespace Opus.Localisation;

/// <summary>
/// Strongly-typed translation key. Keys are dotted-path strings: <c>menu.battle.start</c>.
/// Wrapping in a struct lets us catch raw-string mistakes at compile time and lint
/// untranslated keys via custom analyzer (planned: GS061).
///
/// Implicit conversion to <see cref="string"/> keeps interop with Content (which stores
/// translation keys as raw strings to stay assembly-independent of Localisation).
/// </summary>
public readonly record struct TranslationKey(string Key)
{
    public static TranslationKey Of(string key) => new(key);

    public override string ToString() => Key;

    public static implicit operator string(TranslationKey key) => key.Key;
}
