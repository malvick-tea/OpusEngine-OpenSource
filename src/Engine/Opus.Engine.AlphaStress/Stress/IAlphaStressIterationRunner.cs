using Opus.Engine.AlphaHarness.Smoke;

namespace Opus.Engine.AlphaStress.Stress;

/// <summary>
/// Engine-neutral seam the stress harness drives once per iteration. Runtime callers
/// implement this against the M9 alpha host runner; tests implement it as a
/// deterministic fake so the harness orchestrator can be exercised without standing up
/// a real D3D12 device.
/// </summary>
public interface IAlphaStressIterationRunner
{
    /// <summary>Runs one iteration with the supplied smoke profile. Implementations
    /// must not throw past the public surface — exceptions are caught and reported
    /// through <see cref="AlphaStressIterationRunResult.UnhandledException"/> so the
    /// harness can record a structured issue instead of crashing the run.</summary>
    AlphaStressIterationRunResult Run(int iterationIndex, AlphaSmokeProfile iterationProfile);
}
