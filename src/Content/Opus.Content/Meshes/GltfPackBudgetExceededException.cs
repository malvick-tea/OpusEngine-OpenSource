using System;

namespace Opus.Content.Meshes;

/// <summary>
/// Thrown by <see cref="GltfFilePacker.PackToGlb(string, long)"/> when packing a split glTF would
/// require buffering more bytes (the glTF JSON file plus its sidecar buffers) than the caller-supplied
/// in-memory budget allows. Lets a budget-bounded caller — the package validator — skip deep model
/// validation for an oversized asset instead of risking an unbounded read, without confusing the
/// refusal with a malformed-model failure.
/// </summary>
public sealed class GltfPackBudgetExceededException : Exception
{
    public GltfPackBudgetExceededException(long requiredBytes, long budgetBytes)
        : base($"Packing the glTF requires buffering {requiredBytes} bytes, above the {budgetBytes}-byte in-memory budget.")
    {
        RequiredBytes = requiredBytes;
        BudgetBytes = budgetBytes;
    }

    /// <summary>Total bytes the pack would buffer: the glTF JSON file plus its sidecar buffers.</summary>
    public long RequiredBytes { get; }

    /// <summary>The in-memory budget the caller supplied.</summary>
    public long BudgetBytes { get; }
}
