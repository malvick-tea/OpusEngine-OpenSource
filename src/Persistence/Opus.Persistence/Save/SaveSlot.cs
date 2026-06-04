namespace Opus.Persistence;

/// <summary>
/// Logical save slot identifier. Hosts (Win/Android/iOS) map a slot id to a per-platform
/// file path; <c>Opus.Persistence</c> never sees a path.
///
/// Slot 0 = autosave, slots 1..N = manual saves. The host enforces uniqueness; this type
/// only provides equality and ordering.
/// </summary>
public readonly record struct SaveSlot(int Index)
{
    public static readonly SaveSlot Autosave = new(0);

    public bool IsAutosave => Index == 0;

    public override string ToString() => IsAutosave ? "autosave" : $"slot{Index}";
}
